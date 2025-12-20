using Gigs.DTOs;
using Gigs.Types;
using Gigs.Models;
using Gigs.Repositories;
using Gigs.Services.AI;
using Gigs.Services.Image;

namespace Gigs.Services;

public class ArtistService(
    IArtistRepository repository,
    IAiEnrichmentService aiEnrichmentService,
    IImageService imageService,
    IHttpClientFactory httpClientFactory) : IArtistService
{
    public async Task<List<GetArtistResponse>> GetAllAsync()
    {
        var artists = await repository.GetAllAsync();
        return artists.Select(MapToDto).ToList();
    }

    public async Task<GetArtistResponse> EnrichArtistAsync(ArtistId id)
    {
        var artists = await repository.GetAllAsync();
        var artist = artists.FirstOrDefault(a => a.Id == id) 
                     ?? throw new KeyNotFoundException($"Artist with ID {id} not found.");

        // 1. Get Image URL from AI
        var imageUrl = await aiEnrichmentService.EnrichArtistImage(artist.Name);

        if (string.IsNullOrWhiteSpace(imageUrl))
            return MapToDto(artist); // No image found, return as is

        // 2. Download Image
        try 
        {
            var client = httpClientFactory.CreateClient();
            var imageBytes = await client.GetByteArrayAsync(imageUrl);

            // 3. Save Image
            var fileExtension = Path.GetExtension(imageUrl).Split('?')[0]; // simple strip query params
             if (string.IsNullOrWhiteSpace(fileExtension) || fileExtension.Length > 5) 
                 fileExtension = ".jpg"; // fallback

            var fileName = $"{artist.Slug}-{Guid.NewGuid()}{fileExtension}";
            var savedFileName = await imageService.SaveImageAsync(fileName, imageBytes);

            // 4. Update Artist
            artist.ImageUrl = savedFileName;
            await repository.UpdateAsync(artist);
        }
        catch (Exception)
        {
            // Log error? For now just ignore and return original artist
            // In a real app we'd log this.
        }

        return MapToDto(artist);
    }

    private static GetArtistResponse MapToDto(Artist artist)
    {
        return new GetArtistResponse
        {
            Id = artist.Id,
            Name = artist.Name,
            ImageUrl = artist.ImageUrl,
            Slug = artist.Slug
        };
    }
}
