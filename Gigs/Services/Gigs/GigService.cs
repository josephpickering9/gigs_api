using Gigs.DTOs;
using Gigs.Exceptions;
using Gigs.Models;
using Gigs.Repositories;
using Gigs.Services.AI;
using Gigs.Types;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Services;

public class GigService(IGigRepository repository, Database db, IFestivalRepository festivalRepository, IAiEnrichmentService aiService) : IGigService
{
    public async Task<PaginatedResponse<GetGigResponse>> GetAllAsync(GetGigsFilter filter)
    {
        var (gigs, totalCount) = await repository.GetAllAsync(filter);
        return new PaginatedResponse<GetGigResponse>
        {
            Items = gigs.Select(MapToDto).ToList(),
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<GetGigResponse> GetByIdAsync(GigId id)
    {
        var gig = await repository.GetByIdAsync(id);
        if (gig == null)
        {
            throw new NotFoundException($"Gig with ID {id} not found.");
        }
        return MapToDto(gig);
    }

    public async Task<GetGigResponse> CreateAsync(UpsertGigRequest request)
    {
        var venueId = await GetOrCreateVenue(request.VenueId, request.VenueName, request.VenueCity);
        var festivalId = await GetOrCreateFestival(request.FestivalId, request.FestivalName);
        
        var gig = new Gig
        {
            VenueId = venueId,
            FestivalId = festivalId,
            Date = request.Date,
            TicketCost = request.TicketCost,
            TicketType = request.TicketType,
            ImageUrl = request.ImageUrl,
            Slug = Guid.NewGuid().ToString() // Or generate a slug from venue/date
        };

        if (request.Acts.Any())
        {
            foreach (var actRequest in request.Acts)
            {
                var gigArtist = new GigArtist
                {
                    ArtistId = actRequest.ArtistId,
                    IsHeadliner = actRequest.IsHeadliner,
                    Order = actRequest.Order,
                    SetlistUrl = actRequest.SetlistUrl
                };

                await ProcessSetlist(gigArtist, actRequest.Setlist, actRequest.ArtistId);
                
                gig.Acts.Add(gigArtist);
            }
        }

        if (request.Attendees.Any())
        {
            foreach (var personId in request.Attendees)
            {
                gig.Attendees.Add(new GigAttendee
                {
                    PersonId = personId
                });
            }
        }

        await repository.AddAsync(gig);

        var createdGig = await repository.GetByIdAsync(gig.Id);
        return MapToDto(createdGig!);
    }

    public async Task<GetGigResponse> UpdateAsync(GigId id, UpsertGigRequest request)
    {
        var gig = await repository.GetByIdAsync(id);
        if (gig == null)
        {
            throw new NotFoundException($"Gig with ID {id} not found.");
        }

        gig.VenueId = await GetOrCreateVenue(request.VenueId, request.VenueName, request.VenueCity);
        gig.FestivalId = await GetOrCreateFestival(request.FestivalId, request.FestivalName);
        gig.Date = request.Date;
        gig.TicketCost = request.TicketCost;
        gig.TicketType = request.TicketType;
        gig.ImageUrl = request.ImageUrl;

        var requestedArtistIds = request.Acts.Select(a => a.ArtistId).ToHashSet();
        var actsToRemove = gig.Acts.Where(a => !requestedArtistIds.Contains(a.ArtistId)).ToList();
        foreach (var act in actsToRemove)
        {
            gig.Acts.Remove(act);
        }

        foreach (var actRequest in request.Acts)
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

        var requestedAttendeeIds = request.Attendees.ToHashSet();
        var attendeesToRemove = gig.Attendees.Where(a => !requestedAttendeeIds.Contains(a.PersonId)).ToList();
        foreach (var attendee in attendeesToRemove)
        {
            gig.Attendees.Remove(attendee);
        }

        foreach (var personId in request.Attendees)
        {
            var existingAttendee = gig.Attendees.FirstOrDefault(a => a.PersonId == personId);
            if (existingAttendee == null)
            {
                gig.Attendees.Add(new GigAttendee
                {
                    GigId = gig.Id,
                    PersonId = personId
                });
            }
        }

        await repository.UpdateAsync(gig);
        return MapToDto(gig);
    }

    public async Task<GetGigResponse> EnrichGigAsync(GigId id)
    {
        var gig = await db.Gig
            .Include(g => g.Venue)
            .Include(g => g.Acts).ThenInclude(ga => ga.Artist)
            .Include(g => g.Acts).ThenInclude(ga => ga.Songs).ThenInclude(s => s.Song)
            .Include(g => g.Attendees)
            .FirstOrDefaultAsync(g => g.Id == id);
            
        if (gig == null)
        {
            throw new NotFoundException($"Gig with ID {id} not found.");
        }

        var enrichment = await aiService.EnrichGig(gig);

        // Track existing artist names to avoid duplicates
        var existingArtistNames = gig.Acts
            .Where(a => a.Artist != null)
            .Select(a => a.Artist.Name.ToLower())
            .ToHashSet();

        if (enrichment.SupportActs.Any())
        {
            foreach (var actName in enrichment.SupportActs)
            {
                // Skip if already exists
                if (existingArtistNames.Contains(actName.ToLower()))
                    continue;

                var artist = db.Artist.Local.FirstOrDefault(a => a.Name.Equals(actName, StringComparison.CurrentCultureIgnoreCase))
                             ?? await db.Artist.FirstOrDefaultAsync(a => a.Name.ToLower() == actName.ToLower());

                if (artist == null)
                {
                    artist = new Artist
                    {
                        Name = actName,
                        Slug = Guid.NewGuid().ToString()
                    };
                    db.Artist.Add(artist);
                    await db.SaveChangesAsync(); // Save immediately to get the ID
                }

                // Directly add GigArtist to DbContext instead of navigation collection
                db.GigArtist.Add(new GigArtist
                {
                    GigId = gig.Id,
                    ArtistId = artist.Id,
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
                // Get existing song titles for this headliner
                var existingSongTitles = headliner.Songs
                    .Where(s => s.Song != null)
                    .Select(s => s.Song.Title.ToLower())
                    .ToHashSet();

                int order = 1;

                foreach (var songTitle in enrichment.Setlist)
                {
                    // Skip if already exists
                    if (existingSongTitles.Contains(songTitle.ToLower()))
                    {
                        order++;
                        continue;
                    }

                    var song = db.Song.Local.FirstOrDefault(s => s.ArtistId == headliner.ArtistId && s.Title.Equals(songTitle, StringComparison.CurrentCultureIgnoreCase))
                               ?? await db.Song.FirstOrDefaultAsync(s => s.ArtistId == headliner.ArtistId && s.Title.ToLower() == songTitle.ToLower());

                    if (song == null)
                    {
                        song = new Song
                        {
                            ArtistId = headliner.ArtistId,
                            Title = songTitle,
                            Slug = Guid.NewGuid().ToString()
                        };
                        db.Song.Add(song);
                        await db.SaveChangesAsync(); // Save immediately to get the ID
                    }

                    // Directly add GigArtistSong to DbContext instead of navigation collection
                    db.GigArtistSong.Add(new GigArtistSong
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

        await db.SaveChangesAsync();

        return MapToDto(gig);
    }

    public async Task<int> EnrichAllGigsAsync()
    {
        // We need all gigs with their acts and songs to determine if they are missing data
        var allGigs = await db.Gig
            .Include(g => g.Acts).ThenInclude(ga => ga.Songs)
            .OrderByDescending(g => g.Date)
            .ToListAsync();

        var candidates = allGigs.Where(gig => 
            // Condition 1: Missing Support Acts (Contains only headliner or fewer)
            // Note: Some gigs genuinely have no support, but we check if count <= 1 as a heuristic
            gig.Acts.Count <= 1 
            || 
            // Condition 2: Headliner exists but has no songs (Missing setlist)
            gig.Acts.Any(a => a.IsHeadliner && !a.Songs.Any())
        ).ToList();

        var count = 0;
        foreach (var gig in candidates)
        {
            try
            {
                // Re-use logic. Note: EnrichGigAsync refetches the gig, which is fine but slightly inefficient.
                // Given the heavy AI calls, the DB fetch overhead is negligible.
                await EnrichGigAsync(gig.Id);
                count++;
            }
            catch (Exception)
            {
                // Continue with next
            }
        }
        return count;
    }

    public async Task DeleteAsync(GigId id)
    {
        await repository.DeleteAsync(id);
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
        // Clear existing songs from the join table to overwrite with new list
        // This is a simple approach; for more efficiency we could diff them,
        // but since we are re-ordering, full replace is often safer/easier.
        gigArtist.Songs.Clear();

        if (!setlist.Any()) return;

        int order = 1;
        foreach (var songTitle in setlist)
        {
            var song = db.Song.Local.FirstOrDefault(s => s.ArtistId == artistId && s.Title.Equals(songTitle, StringComparison.CurrentCultureIgnoreCase))
                       ?? await db.Song.FirstOrDefaultAsync(s => s.ArtistId == artistId && s.Title.ToLower() == songTitle.ToLower());

            if (song == null)
            {
                song = new Song
                {
                    ArtistId = artistId,
                    Title = songTitle,
                    Slug = Guid.NewGuid().ToString()
                };
                db.Song.Add(song);
                await db.SaveChangesAsync();
            }

            gigArtist.Songs.Add(new GigArtistSong
            {
                SongId = song.Id,
                Order = order++
            });
        }
    }

    private async Task<VenueId> GetOrCreateVenue(VenueId? venueId, string? venueName, string? venueCity)
    {
        if (venueId.HasValue)
        {
            return venueId.Value;
        }

        if (string.IsNullOrWhiteSpace(venueName) || string.IsNullOrWhiteSpace(venueCity))
        {
            throw new ArgumentException("Either VenueId or both VenueName and VenueCity must be provided.");
        }

        var venue = db.Venue.Local.FirstOrDefault(v => v.Name.Equals(venueName, StringComparison.CurrentCultureIgnoreCase) && v.City.Equals(venueCity, StringComparison.CurrentCultureIgnoreCase))
                    ?? await db.Venue.FirstOrDefaultAsync(v => v.Name.ToLower() == venueName.ToLower() && v.City.ToLower() == venueCity.ToLower());

        if (venue == null)
        {
            venue = new Venue
            {
                Name = venueName,
                City = venueCity,
                Slug = Guid.NewGuid().ToString()
            };
            db.Venue.Add(venue);
            await db.SaveChangesAsync();
        }

        return venue.Id;
    }

    private async Task<FestivalId?> GetOrCreateFestival(FestivalId? festivalId, string? festivalName)
    {
        if (festivalId.HasValue) return festivalId.Value;
        
        if (string.IsNullOrWhiteSpace(festivalName)) return null;

        var festival = await festivalRepository.FindByNameAsync(festivalName);
                       
        if (festival == null)
        {
            festival = new Festival
            {
                Name = festivalName,
                Slug = Guid.NewGuid().ToString()
            };
            await festivalRepository.AddAsync(festival);
        }
        
        return festival.Id;
    }
}
