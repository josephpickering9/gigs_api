using Gigs.DTOs;
using Gigs.Exceptions;
using Gigs.Models;
using Gigs.Repositories;
using Gigs.Types;

namespace Gigs.Services;

public class GigService(IGigRepository repository) : IGigService
{
    // Note: We inject Database directly here for transaction/lookup of related entities if needed, 
    // or we could use other repositories (ArtistRepository, etc.) if they existed.
    // For now, I'll assume we might need to lookup Artists to link them.
    // Ideally, we should have IArtistRepository etc, but to keep it scoped to the request, 
    // I'll stick to what's necessary.
    // Actually, looking at the requirements, to "add/update... relating elements", we need to handle acts.

    public async Task<List<GigDto>> GetAllAsync()
    {
        var gigs = await repository.GetAllAsync();
        return gigs.Select(MapToDto).ToList();
    }

    public async Task<GigDto> GetByIdAsync(GigId id)
    {
        var gig = await repository.GetByIdAsync(id);
        if (gig == null)
        {
            throw new NotFoundException($"Gig with ID {id} not found.");
        }
        return MapToDto(gig);
    }

    public async Task<GigDto> CreateAsync(UpsertGigRequest request)
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

        // Handle Acts
        if (request.ArtistIds.Any())
        {
            foreach (var artistId in request.ArtistIds)
            {
                gig.Acts.Add(new GigArtist
                {
                    ArtistId = artistId,
                    // GigId will be set when added to the Gig
                });
            }
        }

        await repository.AddAsync(gig);
        
        // Refetch to get included entities for DTO
        var createdGig = await repository.GetByIdAsync(gig.Id);
        return MapToDto(createdGig!);
    }

    public async Task<GigDto> UpdateAsync(GigId id, UpsertGigRequest request)
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

        // Update Acts - Simple approach: Clear and Add
        // In a real app, we might want to be smarter to preserve existing IDs or properties
        // For this task, complete replacement of the list is often acceptable or expected for "Update"
        
        // We need to manage the collection carefully with EF Core.
        // Since we have the entity tracked with Acts included
        
        // 1. Remove acts not in the new list
        var artistIdsToRemove = gig.Acts.Where(a => !request.ArtistIds.Contains(a.ArtistId)).ToList();
        foreach (var act in artistIdsToRemove)
        {
            gig.Acts.Remove(act);
        }

        // 2. Add acts that are new
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

    public async Task DeleteAsync(GigId id)
    {
        await repository.DeleteAsync(id);
    }

    private static GigDto MapToDto(Gig gig)
    {
        return new GigDto
        {
            Id = gig.Id,
            VenueId = gig.VenueId,
            VenueName = gig.Venue?.Name ?? "Unknown Venue",
            Date = gig.Date,
            TicketCost = gig.TicketCost,
            TicketType = gig.TicketType,
            ImageUrl = gig.ImageUrl,
            Slug = gig.Slug,
            Acts = gig.Acts.Select(a => a.Artist?.Name ?? "Unknown Artist").ToList()
        };
    }
}
