using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Gigs.DTOs;
using Gigs.Models;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Services.Calendar;

public class GoogleCalendarService : IGoogleCalendarService
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

        // Try to load from calendar-specific credentials first
        var credentialsJson = _configuration["GoogleCalendar:CredentialsJson"];
        var credentialsFile = _configuration["GoogleCalendar:CredentialsFile"];

        // Fall back to VertexAI credentials if calendar-specific ones aren't set
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

    public async Task<List<CalendarEventDto>> GetCalendarEventsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var calendarId = _configuration["GoogleCalendar:CalendarId"] ?? "primary";

        var request = _calendarService.Events.List(calendarId);
        request.TimeMinDateTimeOffset = startDate ?? DateTime.UtcNow.AddYears(-5);
        request.TimeMaxDateTimeOffset = endDate ?? DateTime.UtcNow;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        request.MaxResults = 2500;

        var events = await request.ExecuteAsync();
        var eventDtos = new List<CalendarEventDto>();

        foreach (var calendarEvent in events.Items ?? [])
        {
            if (calendarEvent.Start?.DateTimeDateTimeOffset == null && calendarEvent.Start?.Date == null)
                continue;

            var startDateTime = calendarEvent.Start.DateTimeDateTimeOffset?.DateTime ?? DateTime.Parse(calendarEvent.Start.Date!);

            eventDtos.Add(new CalendarEventDto
            {
                Id = calendarEvent.Id,
                Title = calendarEvent.Summary ?? "Untitled Event",
                StartDateTime = startDateTime,
                EndDateTime = calendarEvent.End?.DateTimeDateTimeOffset?.DateTime ?? (calendarEvent.End?.Date != null ? DateTime.Parse(calendarEvent.End.Date) : null),
                Location = calendarEvent.Location,
                Description = calendarEvent.Description
            });
        }

        return eventDtos;
    }

    public async Task<ImportCalendarEventsResponse> ImportEventsAsGigsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var events = await GetCalendarEventsAsync(startDate, endDate);

        int created = 0;
        int updated = 0;

        foreach (var calendarEvent in events)
        {
            try
            {
                var result = await ProcessCalendarEventAsync(calendarEvent);
                if (result.HasValue)
                {
                    // Save changes after each event to avoid concurrency issues
                    await _db.SaveChangesAsync();

                    if (result.Value)
                        created++;
                    else
                        updated++;
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                // Skip this event and continue - likely a duplicate or concurrent modification
                continue;
            }
        }

        return new ImportCalendarEventsResponse
        {
            EventsFound = events.Count,
            GigsCreated = created,
            GigsUpdated = updated,
            Message = $"Successfully processed {events.Count} events: {created} created, {updated} updated"
        };
    }

    private async Task<bool?> ProcessCalendarEventAsync(CalendarEventDto calendarEvent)
    {
        // Parse the event to extract gig information
        var gigInfo = await ParseCalendarEvent(calendarEvent);

        if (gigInfo == null)
        {
            return null; // Event doesn't match an existing venue = not a gig
        }

        // Check if gig already exists
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

        // Clear existing acts - delete them directly to avoid tracking issues
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

            // Clear the navigation property
            existingGig.Acts.Clear();
        }

        // Add headliner
        var headlinerArtist = await GetOrCreateArtistAsync(gigInfo.ArtistName);
        var headlinerGigArtist = new GigArtist
        {
            GigId = existingGig.Id,
            ArtistId = headlinerArtist.Id,
            IsHeadliner = true,
            Order = 0
        };
        _db.GigArtist.Add(headlinerGigArtist);

        // Add support acts if any
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

        return isNew;
    }

    private async Task<GigInfo?> ParseCalendarEvent(CalendarEventDto calendarEvent)
    {
        // Strategy 1: Use location field to match against existing venues
        // Strategy 2: If no location match, check if event title matches an existing artist
        
        var title = calendarEvent.Title.Trim();
        var location = calendarEvent.Location?.Trim();
        var description = calendarEvent.Description?.Trim();

        Venue? venue = null;
        
        // Try to match venue from location
        if (!string.IsNullOrWhiteSpace(location))
        {
            // Location could be in various formats:
            // - "Venue Name"
            // - "Venue Name, City"
            // - "Venue Name, Address, City"
            
            var locationParts = location.Split(',').Select(p => p.Trim()).ToArray();

            // Try contains match first (more flexible)
            venue = await _db.Venue.FirstOrDefaultAsync(v => v.Name.ToLower().Contains(location.ToLower()));

            // If not found and location has multiple parts, try the first part as venue name
            if (venue == null && locationParts.Length > 0)
            {
                var venueName = locationParts[0];
                venue = await _db.Venue.FirstOrDefaultAsync(v => v.Name == venueName);

                // If still not found, try matching venue name and city
                if (venue == null && locationParts.Length > 1)
                {
                    var city = locationParts[^1]; // Last part is usually city
                    venue = await _db.Venue.FirstOrDefaultAsync(v => v.Name == venueName && v.City == city);
                }
            }
        }

        // Strategy 2: If no venue found, check if title matches an existing artist
        if (venue == null)
        {
            // Check if the event title (or cleaned title) matches an artist name
            var potentialArtist = await _db.Artist.FirstOrDefaultAsync(a => a.Name.ToLower() == title.ToLower());
            
            if (potentialArtist == null)
            {
                // Try removing common suffixes
                var cleanedTitle = title
                    .Replace(" (Live)", "", StringComparison.OrdinalIgnoreCase)
                    .Replace(" - Live", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();
                    
                potentialArtist = await _db.Artist.FirstOrDefaultAsync(a => a.Name.ToLower() == cleanedTitle.ToLower());
            }
            
            // If we found a matching artist but no venue, we can't create a gig
            // (gigs require a venue). Return null.
            if (potentialArtist != null && string.IsNullOrWhiteSpace(location))
            {
                return null; // Artist match but no location = can't determine venue
            }
            
            // If artist matched and we have a location, try to extract venue from location string
            if (potentialArtist != null && !string.IsNullOrWhiteSpace(location))
            {
                var locationParts = location.Split(',').Select(p => p.Trim()).ToArray();
                if (locationParts.Length > 0)
                {
                    venue = await _db.Venue.FirstOrDefaultAsync(v => v.Name.ToLower().Contains(locationParts[0].ToLower()));
                }
            }
        }

        if (venue == null)
        {
            return null; // No venue match and no artist match = not a gig
        }

        // Parse artist name from title
        // Remove common patterns like "@ Venue" or "- Venue" from the title
        var artistName = title;

        // Remove venue name from title if present
        artistName = artistName.Replace($" @ {venue.Name}", "", StringComparison.OrdinalIgnoreCase);
        artistName = artistName.Replace($" at {venue.Name}", "", StringComparison.OrdinalIgnoreCase);
        artistName = artistName.Replace($" - {venue.Name}", "", StringComparison.OrdinalIgnoreCase);
        artistName = artistName.Trim();

        if (string.IsNullOrWhiteSpace(artistName))
        {
            artistName = title; // Fall back to full title if cleaning removed everything
        }

        // Parse description for support acts and ticket cost
        List<string> supportActs = new();
        decimal? ticketCost = null;

        if (!string.IsNullOrWhiteSpace(description))
        {
            // Look for support acts
            var supportMatch = Regex.Match(description, @"support:?\s*(.+)", RegexOptions.IgnoreCase);
            if (supportMatch.Success)
            {
                var supports = supportMatch.Groups[1].Value.Split(new[] { ',', '&', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                supportActs.AddRange(supports);
            }

            // Look for ticket cost
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
            Date = DateOnly.FromDateTime(calendarEvent.StartDateTime),
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
        public List<string> SupportActs { get; set; } = new();
        public decimal? TicketCost { get; set; }
    }
}
