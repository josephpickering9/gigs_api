using Gigs.DataModels;
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
        VenueId venueId;
        if (Guid.TryParse(request.VenueId, out var vId))
        {
            venueId = IdFactory.Create<VenueId>(vId);
        }
        else
        {
            var venueName = request.VenueName ?? ResolveNameFromId(request.VenueId);
            if (string.IsNullOrWhiteSpace(venueName))
            {
                return Result.Fail<GetGigResponse>("Venue Name or Valid Venue ID is required.");
            }
            var venueCity = !string.IsNullOrWhiteSpace(request.VenueCity) ? request.VenueCity : "Unknown";
            venueId = await venueRepository.GetOrCreateAsync(venueName, venueCity);
        }

        var headliner = request.Acts.FirstOrDefault(a => a.IsHeadliner);
        if (headliner != null && Guid.TryParse(headliner.ArtistId, out var headlinerArtistId))
        {
            var existingGig = await repository.FindAsync(venueId, request.Date, IdFactory.Create<ArtistId>(headlinerArtistId));
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

        VenueId venueId;
        if (Guid.TryParse(request.VenueId, out var vId))
        {
            venueId = IdFactory.Create<VenueId>(vId);
        }
        else
        {
            var venueName = request.VenueName ?? ResolveNameFromId(request.VenueId);
            if (string.IsNullOrWhiteSpace(venueName))
            {
                return Result.Fail<GetGigResponse>("Venue Name or Valid Venue ID is required.");
            }
            var venueCity = !string.IsNullOrWhiteSpace(request.VenueCity) ? request.VenueCity : "Unknown";
            venueId = await venueRepository.GetOrCreateAsync(venueName, venueCity);
        }

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
        else if (!string.IsNullOrWhiteSpace(request.FestivalId))
        {
            if (Guid.TryParse(request.FestivalId, out var fId))
            {
                gig.FestivalId = IdFactory.Create<FestivalId>(fId);
            }
            else
            {
                var name = ResolveNameFromId(request.FestivalId);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    gig.FestivalId = await festivalRepository.GetOrCreateAsync(name);
                }
                else
                {
                    gig.FestivalId = null;
                }
            }
        }
        else
        {
            gig.FestivalId = null;
        }

        gig.Date = request.Date;
        gig.Order = request.Order;
        gig.TicketCost = request.TicketCost;
        gig.TicketType = request.TicketType;
        gig.ImageUrl = request.ImageUrl;
    }

    private async Task ReconcileActs(Gig gig, List<GigArtistRequest> requestedActs)
    {
        var resolvedActs = new List<(GigArtistRequest Request, ArtistId ArtistId)>();
        foreach (var req in requestedActs)
        {
            ArtistId artistId;
            if (Guid.TryParse(req.ArtistId, out var aId))
            {
                artistId = IdFactory.Create<ArtistId>(aId);
            }
            else
            {
                var name = ResolveNameFromId(req.ArtistId) ?? req.ArtistId;
                artistId = await artistRepository.GetOrCreateAsync(name);
            }
            resolvedActs.Add((req, artistId));
        }

        var requestedArtistIds = resolvedActs.Select(a => a.ArtistId).ToHashSet();

        var actsToRemove = gig.Acts.Where(a => !requestedArtistIds.Contains(a.ArtistId)).ToList();
        foreach (var act in actsToRemove)
        {
            gig.Acts.Remove(act);
        }

        foreach (var (actRequest, artistId) in resolvedActs)
        {
            var existingAct = gig.Acts.FirstOrDefault(a => a.ArtistId == artistId);
            if (existingAct != null)
            {
                existingAct.IsHeadliner = actRequest.IsHeadliner;
                existingAct.Order = actRequest.Order;
                existingAct.SetlistUrl = actRequest.SetlistUrl;
                await ProcessSetlist(existingAct, actRequest.Setlist, artistId);
            }
            else
            {
                var newAct = new GigArtist
                {
                    ArtistId = artistId,
                    GigId = gig.Id,
                    IsHeadliner = actRequest.IsHeadliner,
                    Order = actRequest.Order,
                    SetlistUrl = actRequest.SetlistUrl
                };
                await ProcessSetlist(newAct, actRequest.Setlist, artistId);
                gig.Acts.Add(newAct);
            }
        }
    }

    private async Task ReconcileAttendees(Gig gig, List<string> requestedAttendeeNames)
    {
        var requestedPersonIds = new List<PersonId>();
        foreach (var personName in requestedAttendeeNames)
        {
            var name = ResolveNameFromId(personName);
            if (name != null)
            {
                var personId = await personRepository.GetOrCreateAsync(name);
                requestedPersonIds.Add(personId);
                continue;
            }

            if (Guid.TryParse(personName, out var guid))
            {
                requestedPersonIds.Add(IdFactory.Create<PersonId>(guid));
                continue;
            }

            var personIdByName = await personRepository.GetOrCreateAsync(personName);
            requestedPersonIds.Add(personIdByName);
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

    public async Task<Result<GetGigResponse>> EnrichGigAsync(GigId id, bool forceUpdate = true)
    {
        var gig = await repository.GetByIdAsync(id);
        if (gig == null)
        {
            return Result.NotFound<GetGigResponse>($"Gig with ID {id} not found.");
        }

        var shouldEnrichSetlist = forceUpdate;
        var shouldEnrichImage = forceUpdate;

        if (!forceUpdate)
        {
            var headliner = gig.Acts.FirstOrDefault(a => a.IsHeadliner);
            // If headliner has no songs, we need to enrich setlist
            if (headliner == null || !headliner.Songs.Any())
            {
                shouldEnrichSetlist = true;
            }

            // If no generic image is set (or null), enrich it
            if (string.IsNullOrWhiteSpace(gig.ImageUrl))
            {
                shouldEnrichImage = true;
            }
        }

        // If nothing to do, return early
        if (!shouldEnrichSetlist && !shouldEnrichImage)
        {
            return MapToDto(gig).ToSuccess();
        }

        var enrichmentResult = await aiService.EnrichGig(gig, shouldEnrichSetlist, shouldEnrichImage);
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

                foreach (var enrichedSong in enrichment.Setlist)
                {
                    if (existingSongTitles.Contains(enrichedSong.Title.ToLower()))
                    {
                        var existingLink = headliner.Songs.FirstOrDefault(s => s.Song?.Title.ToLower() == enrichedSong.Title.ToLower());
                        if (existingLink != null)
                        {
                            existingLink.Order = order;
                            existingLink.IsEncore = enrichedSong.IsEncore;
                            existingLink.Info = enrichedSong.Info;
                            existingLink.IsTape = enrichedSong.IsTape;
                            
                            if (!string.IsNullOrWhiteSpace(enrichedSong.WithArtistName))
                            {
                                existingLink.WithArtistId = await artistRepository.GetOrCreateAsync(enrichedSong.WithArtistName);
                            }
                            
                            if (!string.IsNullOrWhiteSpace(enrichedSong.CoverArtistName))
                            {
                                existingLink.CoverArtistId = await artistRepository.GetOrCreateAsync(enrichedSong.CoverArtistName);
                            }
                        }
                        order++;
                        continue;
                    }

                    var song = await songRepository.GetOrCreateAsync(headliner.ArtistId, enrichedSong.Title);

                    ArtistId? withArtistId = null;
                    if (!string.IsNullOrWhiteSpace(enrichedSong.WithArtistName))
                    {
                        withArtistId = await artistRepository.GetOrCreateAsync(enrichedSong.WithArtistName);
                    }
                    
                    ArtistId? coverArtistId = null;
                    if (!string.IsNullOrWhiteSpace(enrichedSong.CoverArtistName))
                    {
                        coverArtistId = await artistRepository.GetOrCreateAsync(enrichedSong.CoverArtistName);
                    }

                    headliner.Songs.Add(new GigArtistSong
                    {
                        GigArtistId = headliner.Id,
                        SongId = song.Id,
                        Order = order,
                        IsEncore = enrichedSong.IsEncore,
                        Info = enrichedSong.Info,
                        IsTape = enrichedSong.IsTape,
                        WithArtistId = withArtistId,
                        CoverArtistId = coverArtistId
                    });

                    existingSongTitles.Add(enrichedSong.Title.ToLower());
                    order++;
                }
            }
        }

        // Update image URL if found
        if (!string.IsNullOrWhiteSpace(enrichment.ImageSearchQuery) && 
            Uri.TryCreate(enrichment.ImageSearchQuery, UriKind.Absolute, out var imageUri) &&
            (imageUri.Scheme == Uri.UriSchemeHttp || imageUri.Scheme == Uri.UriSchemeHttps))
        {
            gig.ImageUrl = enrichment.ImageSearchQuery;
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
                await EnrichGigAsync(gig.Id, forceUpdate: false);
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
            Order = gig.Order,
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
                Setlist = a.Songs.OrderBy(s => s.Order).Select(s => new GetGigSongResponse 
                {
                    Title = s.Song.Title,
                    Order = s.Order,
                    IsEncore = s.IsEncore
                }).ToList()
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
        if (!setlist.Any()) return;

        var existingSongs = gigArtist.Songs.Where(s => s.Song != null).ToDictionary(s => s.Song.Title.ToLower(), s => s);

        int order = 1;
        foreach (var songTitle in setlist)
        {
            if (existingSongs.TryGetValue(songTitle.ToLower(), out var existingSong))
            {
                existingSong.Order = order++;
                // Manual setlist updates via this method don't currently support setting IsEncore explicitly 
                // unless we change the request DTO. For now, preserve existing value or default?
                // Preserving existing IsEncore seems safest if just reordering.
            }
            else
            {
                var song = await songRepository.GetOrCreateAsync(artistId, songTitle);
                gigArtist.Songs.Add(new GigArtistSong
                {
                    GigArtistId = gigArtist.Id,
                    SongId = song.Id,
                    Order = order++,
                    IsEncore = false // Default for manual entry via string list
                });
            }
        }
    }
    private static string? ResolveNameFromId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (id.StartsWith("new:", StringComparison.OrdinalIgnoreCase))
        {
            return id.Substring(4);
        }
        return null;
    }
}
