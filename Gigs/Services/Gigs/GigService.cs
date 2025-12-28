using Gigs.DTOs;
using Gigs.Exceptions;
using Gigs.Models;
using Gigs.Repositories;
using Gigs.Services.AI;
using Gigs.Types;

namespace Gigs.Services;

public class GigService(
    GigRepository repository,
    ArtistRepository artistRepository,
    VenueRepository venueRepository,
    FestivalRepository festivalRepository,
    PersonRepository personRepository,
    SongRepository songRepository,
    AiEnrichmentService aiService)
{
    public async Task<Result<PaginatedResponse<GetGigResponse>>> GetAllAsync(GetGigsFilter filter)
    {
        var (gigs, totalCount) = await repository.GetAllAsync(filter);
        var response = new PaginatedResponse<GetGigResponse>
        {
            Items = gigs.Select(MapToDto).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = totalCount
        };
        return response.ToSuccess();
    }

    public async Task<Result<GetGigResponse>> GetByIdAsync(GigId id)
    {
        var gig = await repository.GetByIdAsync(id);
        if (gig == null)
        {
            return Result.NotFound<GetGigResponse>($"Gig with ID {id} not found.");
        }

        return MapToDto(gig).ToSuccess();
    }

    public async Task<Result<GetGigResponse>> CreateAsync(UpsertGigRequest request)
    {
        var venueId = await venueRepository.GetOrCreateAsync(request.VenueName!, request.VenueCity!);

        var headliner = request.Acts.FirstOrDefault(a => a.IsHeadliner);
        if (headliner != null)
        {
            var existingGig = await repository.FindAsync(venueId, request.Date, headliner.ArtistId);
            if (existingGig != null)
            {
                return await UpdateAsync(existingGig.Id, request);
            }
        }

        var gig = new Gig
        {
            Slug = Guid.NewGuid().ToString()
        };

        await UpdateGigDetails(gig, request, venueId);
        await ReconcileActs(gig, request.Acts);
        await ReconcileAttendees(gig, request.Attendees);

        await repository.AddAsync(gig);

        var createdGig = await repository.GetByIdAsync(gig.Id);
        return MapToDto(createdGig!).ToSuccess();
    }

    public async Task<Result<GetGigResponse>> UpdateAsync(GigId id, UpsertGigRequest request)
    {
        var gig = await repository.GetByIdAsync(id);
        if (gig == null)
        {
            return Result.NotFound<GetGigResponse>($"Gig with ID {id} not found.");
        }

        var venueId = await venueRepository.GetOrCreateAsync(request.VenueName!, request.VenueCity!);

        await UpdateGigDetails(gig, request, venueId);
        await ReconcileActs(gig, request.Acts);
        await ReconcileAttendees(gig, request.Attendees);

        await repository.UpdateAsync(gig);
        return MapToDto(gig).ToSuccess();
    }

    private async Task UpdateGigDetails(Gig gig, UpsertGigRequest request, VenueId venueId)
    {
        gig.VenueId = venueId;

        if (!string.IsNullOrWhiteSpace(request.FestivalName))
        {
            gig.FestivalId = await festivalRepository.GetOrCreateAsync(request.FestivalName);
        }
        else if (request.FestivalId.HasValue)
        {
            gig.FestivalId = request.FestivalId;
        }
        else
        {
            gig.FestivalId = null;
        }

        gig.Date = request.Date;
        gig.TicketCost = request.TicketCost;
        gig.TicketType = request.TicketType;
        gig.ImageUrl = request.ImageUrl;
    }

    private async Task ReconcileActs(Gig gig, List<GigArtistRequest> requestedActs)
    {
        var requestedArtistIds = requestedActs.Select(a => a.ArtistId).ToHashSet();

        var actsToRemove = gig.Acts.Where(a => !requestedArtistIds.Contains(a.ArtistId)).ToList();
        foreach (var act in actsToRemove)
        {
            gig.Acts.Remove(act);
        }

        foreach (var actRequest in requestedActs)
        {
            var existingAct = gig.Acts.FirstOrDefault(a => a.ArtistId == actRequest.ArtistId);
            if (existingAct != null)
            {
                existingAct.IsHeadliner = actRequest.IsHeadliner;
                existingAct.Order = actRequest.Order;
                existingAct.SetlistUrl = actRequest.SetlistUrl;
                await ProcessSetlist(existingAct, actRequest.Setlist, actRequest.ArtistId);
            }
            else
            {
                var newAct = new GigArtist
                {
                    ArtistId = actRequest.ArtistId,
                    GigId = gig.Id,
                    IsHeadliner = actRequest.IsHeadliner,
                    Order = actRequest.Order,
                    SetlistUrl = actRequest.SetlistUrl
                };
                await ProcessSetlist(newAct, actRequest.Setlist, actRequest.ArtistId);
                gig.Acts.Add(newAct);
            }
        }
    }

    private async Task ReconcileAttendees(Gig gig, List<string> requestedAttendeeNames)
    {
        var requestedPersonIds = new List<PersonId>();
        foreach (var personName in requestedAttendeeNames)
        {
            var personId = await personRepository.GetOrCreateAsync(personName);
            requestedPersonIds.Add(personId);
        }

        var requestedAttendeeIds = requestedPersonIds.ToHashSet();

        var attendeesToRemove = gig.Attendees.Where(a => !requestedAttendeeIds.Contains(a.PersonId)).ToList();
        foreach (var attendee in attendeesToRemove)
        {
            gig.Attendees.Remove(attendee);
        }

        foreach (var personId in requestedPersonIds)
        {
            var existingAttendee = gig.Attendees.FirstOrDefault(a => a.PersonId == personId);
            if (existingAttendee == null)
            {
                gig.Attendees.Add(new GigAttendee
                {
                    PersonId = personId
                });
            }
        }
    }

    public async Task<Result<GetGigResponse>> EnrichGigAsync(GigId id)
    {
        var gig = await repository.GetByIdAsync(id);
        if (gig == null)
        {
            return Result.NotFound<GetGigResponse>($"Gig with ID {id} not found.");
        }

        var enrichmentResult = await aiService.EnrichGig(gig);
        var enrichment = (enrichmentResult.IsSuccess && enrichmentResult.Data != null) ? enrichmentResult.Data : new AiEnrichmentResult();

        var existingArtistNames = gig.Acts
            .Where(a => a.Artist != null)
            .Select(a => a.Artist.Name.ToLower())
            .ToHashSet();

        if (enrichment.SupportActs.Any())
        {
            foreach (var actName in enrichment.SupportActs)
            {
                if (existingArtistNames.Contains(actName.ToLower()))
                    continue;

                var artistId = await artistRepository.GetOrCreateAsync(actName);

                gig.Acts.Add(new GigArtist
                {
                    GigId = gig.Id,
                    ArtistId = artistId,
                    IsHeadliner = false,
                    Order = 0
                });

                existingArtistNames.Add(actName.ToLower());
            }
        }

        if (enrichment.Setlist.Any())
        {
            var headliner = gig.Acts.FirstOrDefault(a => a.IsHeadliner);
            if (headliner != null)
            {
                var existingSongTitles = headliner.Songs
                    .Where(s => s.Song != null)
                    .Select(s => s.Song.Title.ToLower())
                    .ToHashSet();

                int order = 1;

                foreach (var songTitle in enrichment.Setlist)
                {
                    if (existingSongTitles.Contains(songTitle.ToLower()))
                    {
                        order++;
                        continue;
                    }

                    var song = await songRepository.GetOrCreateAsync(headliner.ArtistId, songTitle);

                    headliner.Songs.Add(new GigArtistSong
                    {
                        GigArtistId = headliner.Id,
                        SongId = song.Id,
                        Order = order
                    });

                    existingSongTitles.Add(songTitle.ToLower());
                    order++;
                }
            }
        }

        await repository.UpdateAsync(gig);

        return MapToDto(gig).ToSuccess();
    }

    public async Task<Result<int>> EnrichAllGigsAsync()
    {
        var candidates = await repository.GetEnrichmentCandidatesAsync();
        var count = 0;
        foreach (var gig in candidates)
        {
            try
            {
                await EnrichGigAsync(gig.Id);
                count++;
            }
            catch (Exception)
            {
                // Continue with next
            }
        }

        return count.ToSuccess();
    }

    public async Task<Result<bool>> DeleteAsync(GigId id)
    {
        var gig = await repository.GetByIdAsync(id);
        if (gig == null)
        {
            return Result.NotFound<bool>($"Gig with ID {id} not found.");
        }

        await repository.DeleteAsync(id);
        return true.ToSuccess();
    }

    private static GetGigResponse MapToDto(Gig gig)
    {
        return new GetGigResponse
        {
            Id = gig.Id,
            VenueId = gig.VenueId,
            VenueName = gig.Venue?.Name ?? "Unknown Venue",
            FestivalId = gig.FestivalId,
            FestivalName = gig.Festival?.Name,
            Date = gig.Date,
            TicketCost = gig.TicketCost,
            TicketType = gig.TicketType,
            ImageUrl = gig.ImageUrl ?? gig.Acts.FirstOrDefault(a => a.IsHeadliner)?.Artist?.ImageUrl ?? gig.Venue?.ImageUrl,
            Slug = gig.Slug,
            Acts = gig.Acts.Select(a => new GetGigArtistResponse
            {
                ArtistId = a.ArtistId,
                Name = a.Artist?.Name ?? "Unknown Artist",
                IsHeadliner = a.IsHeadliner,
                ImageUrl = a.Artist?.ImageUrl,
                Setlist = a.Songs.OrderBy(s => s.Order).Select(s => s.Song.Title).ToList()
            }).OrderByDescending(a => a.IsHeadliner).ThenBy(a => a.Name).ToList(),
            Attendees = gig.Attendees.Select(a => new GetGigAttendeeResponse
            {
                PersonId = a.PersonId,
                PersonName = a.Person?.Name ?? "Unknown Person"
            }).ToList()
        };
    }

    private async Task ProcessSetlist(GigArtist gigArtist, List<string> setlist, ArtistId artistId)
    {
        gigArtist.Songs.Clear();

        if (!setlist.Any()) return;

        int order = 1;
        foreach (var songTitle in setlist)
        {
            var song = await songRepository.GetOrCreateAsync(artistId, songTitle);

            gigArtist.Songs.Add(new GigArtistSong
            {
                SongId = song.Id,
                Order = order++
            });
        }
    }
}
