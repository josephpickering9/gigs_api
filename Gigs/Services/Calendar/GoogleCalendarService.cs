using System.Text.RegularExpressions;
using Gigs.DataModels;
using Gigs.Models;
using Gigs.Types;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Services.Calendar;

public class GoogleCalendarService : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly Database _db;
    private readonly CalendarService _calendarService;

    public GoogleCalendarService(IConfiguration configuration, Database db)
    {
        _configuration = configuration;
        _db = db;
        _calendarService = InitializeCalendarService();
    }

    private CalendarService InitializeCalendarService()
    {
        GoogleCredential credential;

        var credentialsJson = _configuration["GoogleCalendar:CredentialsJson"];
        var credentialsFile = _configuration["GoogleCalendar:CredentialsFile"];

        if (string.IsNullOrWhiteSpace(credentialsJson))
        {
            credentialsJson = _configuration["VertexAi:CredentialsJson"];
        }

        if (string.IsNullOrWhiteSpace(credentialsFile))
        {
            credentialsFile = _configuration["VertexAi:CredentialsFile"];
        }

        if (!string.IsNullOrWhiteSpace(credentialsJson))
        {
            credential = GoogleCredential.FromJson(credentialsJson)
                .CreateScoped(CalendarService.Scope.CalendarReadonly);
        }
        else if (!string.IsNullOrWhiteSpace(credentialsFile))
        {
            if (!File.Exists(credentialsFile))
            {
                throw new FileNotFoundException($"Google Calendar credentials file not found: {credentialsFile}");
            }

            credential = GoogleCredential.FromFile(credentialsFile)
                .CreateScoped(CalendarService.Scope.CalendarReadonly);
        }
        else
        {
            throw new InvalidOperationException(
                "Google Calendar credentials not configured. Please set either GoogleCalendar or VertexAi credentials (CredentialsJson or CredentialsFile)");
        }

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Gigs App"
        });
    }

    public async Task<Result<List<GetCalendarEventResponse>>> GetCalendarEventsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var calendarId = _configuration["GoogleCalendar:CalendarId"] ?? "primary";

            var request = _calendarService.Events.List(calendarId);
            request.TimeMinDateTimeOffset = startDate ?? DateTime.UtcNow.AddYears(-5);
            request.TimeMaxDateTimeOffset = endDate ?? DateTime.UtcNow;
            request.SingleEvents = true;
            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            request.MaxResults = 2500;

            var events = await request.ExecuteAsync();
            var eventDtos = new List<GetCalendarEventResponse>();

            foreach (var calendarEvent in events.Items ??[])
            {
                if (calendarEvent.Start?.DateTimeDateTimeOffset == null && calendarEvent.Start?.Date == null)
                    continue;

                var startDateTime = calendarEvent.Start.DateTimeDateTimeOffset?.DateTime ?? DateTime.Parse(calendarEvent.Start.Date!);

                eventDtos.Add(new GetCalendarEventResponse
                {
                    Id = calendarEvent.Id,
                    Title = calendarEvent.Summary ?? "Untitled Event",
                    StartDateTime = startDateTime,
                    EndDateTime = calendarEvent.End?.DateTimeDateTimeOffset?.DateTime ?? (calendarEvent.End?.Date != null ? DateTime.Parse(calendarEvent.End.Date) : null),
                    Location = calendarEvent.Location,
                    Description = calendarEvent.Description
                });
            }

            return eventDtos.ToSuccess();
        }
        catch (Exception ex)
        {
            return Result.Fail<List<GetCalendarEventResponse>>($"Error fetching calendar events: {ex.Message}");
        }
    }

    public async Task<Result<ImportCalendarEventsResponse>> ImportEventsAsGigsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var eventsResult = await GetCalendarEventsAsync(startDate, endDate);
            if (!eventsResult.IsSuccess)
            {
                return Result.Fail<ImportCalendarEventsResponse>(eventsResult.Error?.Message ?? "Failed to fetch events");
            }

            var events = eventsResult.Data!;

            int created = 0;
            int updated = 0;
            int skipped = 0;
            int venuesAtStart = await _db.Venue.CountAsync();

            foreach (var calendarEvent in events)
            {
                try
                {
                    var result = await ProcessCalendarEventAsync(calendarEvent);
                    if (result.HasValue)
                    {
                        await _db.SaveChangesAsync();

                        if (result.Value)
                            created++;
                        else
                            updated++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    skipped++;
                    continue;
                }
            }

            int venuesCreated = await _db.Venue.CountAsync() - venuesAtStart;

            return new ImportCalendarEventsResponse
            {
                EventsFound = events.Count,
                GigsCreated = created,
                GigsUpdated = updated,
                EventsSkipped = skipped,
                VenuesCreated = venuesCreated,
                Message = $"Processed {events.Count} events: {created} created, {updated} updated, {skipped} skipped, {venuesCreated} venues created"
            }.ToSuccess();
        }
        catch (Exception ex)
        {
            return Result.Fail<ImportCalendarEventsResponse>($"Error importing calendar events: {ex.Message}");
        }
    }

    private async Task<bool?> ProcessCalendarEventAsync(GetCalendarEventResponse getCalendarEvent)
    {
        var gigInfo = await ParseCalendarEvent(getCalendarEvent);

        if (gigInfo == null)
        {
            return null;
        }

        var existingGig = await _db.Gig
            .Include(g => g.Acts)
            .FirstOrDefaultAsync(g => g.Date == gigInfo.Date && g.VenueId == gigInfo.Venue.Id);

        bool isNew = existingGig == null;

        if (isNew)
        {
            existingGig = new Gig
            {
                Date = gigInfo.Date,
                VenueId = gigInfo.Venue.Id,
                Slug = Guid.NewGuid().ToString()
            };
            _db.Gig.Add(existingGig);
        }

        // Update gig details
        if (gigInfo.TicketCost.HasValue)
            existingGig.TicketCost = gigInfo.TicketCost;

        if (isNew || !existingGig.Acts.Any())
        {
            if (!isNew)
            {
                var existingActIds = await _db.GigArtist
                    .Where(ga => ga.GigId == existingGig.Id)
                    .Select(ga => ga.Id)
                    .ToListAsync();

                if (existingActIds.Any())
                {
                    await _db.GigArtist
                        .Where(ga => existingActIds.Contains(ga.Id))
                        .ExecuteDeleteAsync();
                }

                existingGig.Acts.Clear();
            }

            var headlinerArtist = await GetOrCreateArtistAsync(gigInfo.ArtistName);

            var headlinerGigArtist = new GigArtist
            {
                GigId = existingGig.Id,
                ArtistId = headlinerArtist.Id,
                IsHeadliner = true,
                Order = 0
            };
            _db.GigArtist.Add(headlinerGigArtist);

            int order = 1;
            foreach (var supportName in gigInfo.SupportActs)
            {
                var supportArtist = await GetOrCreateArtistAsync(supportName);
                var supportGigArtist = new GigArtist
                {
                    GigId = existingGig.Id,
                    ArtistId = supportArtist.Id,
                    IsHeadliner = false,
                    Order = order++
                };
                _db.GigArtist.Add(supportGigArtist);
            }
        }

        return isNew;
    }

    private async Task<GigInfo?> ParseCalendarEvent(GetCalendarEventResponse getCalendarEvent)
    {
        var title = getCalendarEvent.Title.Trim();

        var location = getCalendarEvent.Location?.Trim();
        var description = getCalendarEvent.Description?.Trim();

        Venue? venue = null;
        Artist? matchedArtist = null;

        if (!string.IsNullOrWhiteSpace(title))
        {
            matchedArtist = await _db.Artist.FirstOrDefaultAsync(a => a.Name.ToLower() == title.ToLower());

            if (matchedArtist == null)
            {
                var cleanedTitle = title

                    .Replace(" (Live)", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace(" - Live", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Trim();

                matchedArtist = await _db.Artist.FirstOrDefaultAsync(a => a.Name.ToLower() == cleanedTitle.ToLower());
            }

            if (matchedArtist == null && title.Contains(" @ "))
            {
                var artistPart = title.Split(" @ ")[0].Trim();
                matchedArtist = await _db.Artist.FirstOrDefaultAsync(a => a.Name.ToLower() == artistPart.ToLower());
            }
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var locationParts = location.Split(',').Select(p => p.Trim()).ToArray();

            venue = await _db.Venue.FirstOrDefaultAsync(v => v.Name.ToLower() == location.ToLower());

            if (venue == null && locationParts.Length > 0)
            {
                var venueName = locationParts[0];
                venue = await _db.Venue.FirstOrDefaultAsync(v => v.Name.ToLower() == venueName.ToLower());

                if (venue == null && locationParts.Length > 1)
                {
                    var city = locationParts[^1];
                    venue = await _db.Venue.FirstOrDefaultAsync(v =>
                        v.Name.ToLower() == venueName.ToLower() && v.City.ToLower() == city.ToLower());
                }
            }
        }

        if (matchedArtist == null && venue == null)
        {
            return null;
        }

        if (matchedArtist != null && venue == null)
        {
            return null;
        }

        var artistName = title;

        if (title.Contains(" @ "))
        {
            artistName = title.Split(" @ ")[0].Trim();
        }
        else if (title.Contains(" at "))
        {
            artistName = title.Split(" at ")[0].Trim();
        }

        if (string.IsNullOrWhiteSpace(artistName))
        {
            artistName = title;
        }

        List<string> supportActs = new ();
        decimal? ticketCost = null;

        if (!string.IsNullOrWhiteSpace(description))
        {
            var supportMatch = Regex.Match(description, @"support:?\s*(.+)", RegexOptions.IgnoreCase);
            if (supportMatch.Success)
            {
                var supports = supportMatch.Groups[1].Value.Split(new[] { ',', '&', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                supportActs.AddRange(supports);
            }

            var costMatch = Regex.Match(description, @"[£$]\s*(\d+(?:\.\d{2})?)", RegexOptions.IgnoreCase);
            if (costMatch.Success && decimal.TryParse(costMatch.Groups[1].Value, out var cost))
            {
                ticketCost = cost;
            }
        }

        return new GigInfo
        {
            ArtistName = artistName,
            Venue = venue,
            Date = DateOnly.FromDateTime(getCalendarEvent.StartDateTime),
            SupportActs = supportActs,
            TicketCost = ticketCost
        };
    }

    private async Task<Artist> GetOrCreateArtistAsync(string name)
    {
        var artist = await _db.Artist.FirstOrDefaultAsync(a => a.Name == name);
        if (artist == null)
        {
            artist = _db.Artist.Local.FirstOrDefault(a => a.Name == name);
        }

        if (artist == null)
        {
            artist = new Artist
            {
                Name = name,
                Slug = Guid.NewGuid().ToString()
            };
            _db.Artist.Add(artist);
        }

        return artist;
    }

    private class GigInfo
    {
        public string ArtistName { get; set; } = null!;
        public Venue Venue { get; set; } = null!;
        public DateOnly Date { get; set; }
        public List<string> SupportActs { get; set; } = new ();
        public decimal? TicketCost { get; set; }
    }

    public void Dispose()
    {
        _calendarService.Dispose();
    }
}
