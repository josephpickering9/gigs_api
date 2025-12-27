using Gigs.DTOs;
using Gigs.Types;
using Gigs.Models;
using Gigs.Repositories;
using Gigs.Services.AI;
using Gigs.Services.Image;

namespace Gigs.Services;

public class VenueService(
    VenueRepository repository,
    AiEnrichmentService aiEnrichmentService,
    ImageService imageService,
    IHttpClientFactory httpClientFactory)
{
    public async Task<List<GetVenueResponse>> GetAllAsync()
    {
        var venues = await repository.GetAllAsync();
        return venues.Select(MapToDto).ToList();
    }

    public async Task<GetVenueResponse> EnrichVenueAsync(VenueId id)
    {
        var venues = await repository.GetAllAsync();
        var venue = venues.FirstOrDefault(v => v.Id == id) 
                    ?? throw new KeyNotFoundException($"Venue with ID {id} not found.");

        // 1. Get Image URL from AI
        var imageUrl = await aiEnrichmentService.EnrichVenueImage(venue.Name, venue.City);

        if (string.IsNullOrWhiteSpace(imageUrl))
            return MapToDto(venue); // No image found, return as is

        // 2. Download Image
        try 
        {
            var client = httpClientFactory.CreateClient();
            var imageBytes = await client.GetByteArrayAsync(imageUrl);

            // 3. Save Image
            var fileExtension = Path.GetExtension(imageUrl).Split('?')[0]; // simple strip query params
            if (string.IsNullOrWhiteSpace(fileExtension) || fileExtension.Length > 5) 
                fileExtension = ".jpg"; // fallback

            var fileName = $"{venue.Slug}-{Guid.NewGuid()}{fileExtension}";
            var savedFileName = await imageService.SaveImageAsync(fileName, imageBytes);

            // 4. Update Venue
            venue.ImageUrl = savedFileName;
            await repository.UpdateAsync(venue);
        }
        catch (Exception)
        {
            // Log error? For now just ignore and return original venue
        }

        return MapToDto(venue);
    }

    public async Task<int> EnrichAllVenuesAsync()
    {
        var venues = await repository.GetAllAsync();
        var missingDataVenues = venues.Where(v => string.IsNullOrWhiteSpace(v.ImageUrl)).ToList();
        var count = 0;

        foreach (var venue in missingDataVenues)
        {
            try
            {
                await EnrichVenueAsync(venue.Id);
                count++;
            }
            catch (Exception)
            {
                // Continue with next venue
            }
        }
        return count;
    }

    private static GetVenueResponse MapToDto(Venue venue)
    {
        return new GetVenueResponse
        {
            Id = venue.Id,
            Name = venue.Name,
            City = venue.City,
            ImageUrl = venue.ImageUrl,
            Slug = venue.Slug
        };
    }
}
