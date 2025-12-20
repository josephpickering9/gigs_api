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
    IHttpClientFactory httpClientFactory,
    Gigs.Services.External.ISpotifyService spotifyService) : IArtistService
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

        // 1. Get Image URL from Spotify
        var imageUrl = await spotifyService.GetArtistImageAsync(artist.Name);

        // Fallback to AI if Spotify fails
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
             imageUrl = await aiEnrichmentService.EnrichArtistImage(artist.Name);
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
            return MapToDto(artist); // No image found, return as is

        // 2. Download Image
        try
        {
            var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync(imageUrl);

            if (!response.IsSuccessStatusCode)
            {
                // Log warning or return?
                return MapToDto(artist);
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/"))
            {
                // Not an image (likely an HTML page or error page)
                return MapToDto(artist);
            }

            var imageBytes = await response.Content.ReadAsByteArrayAsync();

            // 3. Save Image
            var fileExtension = Path.GetExtension(imageUrl).Split('?')[0];

            // If original extension is not useful, try to infer from content type
            if (string.IsNullOrWhiteSpace(fileExtension) || fileExtension.Length > 5)
            {
                switch (contentType)
                {
                    case "image/jpeg": fileExtension = ".jpg"; break;
                    case "image/png": fileExtension = ".png"; break;
                    case "image/gif": fileExtension = ".gif"; break;
                    case "image/webp": fileExtension = ".webp"; break;
                    default: fileExtension = ".jpg"; break; // Fallback
                }
            }

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

    public async Task<int> EnrichAllArtistsAsync()
    {
        var artists = await repository.GetAllAsync();
        var missingDataArtists = artists.Where(a => string.IsNullOrWhiteSpace(a.ImageUrl)).ToList();
        var count = 0;

        foreach (var artist in missingDataArtists)
        {
            try
            {
                await EnrichArtistAsync(artist.Id);
                count++;
            }
            catch (Exception)
            {
                // Continue with next artist even if one fails
            }
        }

        return count;
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
