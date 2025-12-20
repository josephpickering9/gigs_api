using Gigs.DTOs;
using Gigs.Exceptions;
using Gigs.Models;
using Gigs.Repositories;
using Gigs.Services.AI;
using Gigs.Types;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Services;

public class GigService(IGigRepository repository, Database db, IAiEnrichmentService aiService) : IGigService
{
    public async Task<List<GetGigResponse>> GetAllAsync(GetGigsFilter filter)
    {
        var gigs = await repository.GetAllAsync(filter);
        return gigs.Select(MapToDto).ToList();
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
        var gig = new Gig
        {
            VenueId = request.VenueId,
            Date = request.Date,
            TicketCost = request.TicketCost,
            TicketType = request.TicketType,
            ImageUrl = request.ImageUrl,
            Slug = Guid.NewGuid().ToString() // Or generate a slug from venue/date
        };

        if (request.ArtistIds.Any())
        {
            foreach (var artistId in request.ArtistIds)
            {
                gig.Acts.Add(new GigArtist
                {
                    ArtistId = artistId,
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

        gig.VenueId = request.VenueId;
        gig.Date = request.Date;
        gig.TicketCost = request.TicketCost;
        gig.TicketType = request.TicketType;
        gig.ImageUrl = request.ImageUrl;

        var artistIdsToRemove = gig.Acts.Where(a => !request.ArtistIds.Contains(a.ArtistId)).ToList();
        foreach (var act in artistIdsToRemove)
        {
            gig.Acts.Remove(act);
        }

        var existingArtistIds = gig.Acts.Select(a => a.ArtistId).ToHashSet();
        foreach (var artistId in request.ArtistIds)
        {
            if (!existingArtistIds.Contains(artistId))
            {
                gig.Acts.Add(new GigArtist { ArtistId = artistId, GigId = gig.Id });
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
            Date = gig.Date,
            TicketCost = gig.TicketCost,
            TicketType = gig.TicketType,
            ImageUrl = gig.ImageUrl,
            Slug = gig.Slug,
            Acts = gig.Acts.Select(a => new GetGigArtistResponse
            {
                ArtistId = a.ArtistId,
                Name = a.Artist?.Name ?? "Unknown Artist",
                IsHeadliner = a.IsHeadliner,
                ImageUrl = a.Artist?.ImageUrl
            }).OrderByDescending(a => a.IsHeadliner).ThenBy(a => a.Name).ToList()
        };
    }
}
