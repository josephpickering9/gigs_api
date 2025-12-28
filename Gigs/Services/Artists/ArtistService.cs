using Gigs.DataModels;
using Gigs.Models;
using Gigs.Repositories;
using Gigs.Services.AI;
using Gigs.Services.External;
using Gigs.Services.Image;
using Gigs.Types;

namespace Gigs.Services;

public class ArtistService(
    ArtistRepository repository,
    AiEnrichmentService aiEnrichmentService,
    ImageService imageService,
    IHttpClientFactory httpClientFactory,
    SpotifyService spotifyService)
{
    public async Task<Result<List<GetArtistResponse>>> GetAllAsync()
    {
        var artists = await repository.GetAllAsync();
        return artists.Select(MapToDto).ToList().ToSuccess();
    }

    public async Task<Result<GetArtistResponse>> EnrichArtistAsync(ArtistId id)
    {
        var artists = await repository.GetAllAsync();
        var artist = artists.FirstOrDefault(a => a.Id == id);

        if (artist == null)
        {
            return Result.NotFound<GetArtistResponse>($"Artist with ID {id} not found.");
        }

        var spotifyResult = await spotifyService.GetArtistImageAsync(artist.Name);
        var imageUrl = spotifyResult.IsSuccess ? spotifyResult.Data : null;

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            var aiResult = await aiEnrichmentService.EnrichArtistImage(artist.Name);
            imageUrl = aiResult.IsSuccess ? aiResult.Data : null;
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
            return MapToDto(artist).ToSuccess();

        try
        {
            var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync(imageUrl);

            if (!response.IsSuccessStatusCode)
            {
                return MapToDto(artist).ToSuccess();
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrWhiteSpace(contentType) || !contentType.StartsWith("image/"))
            {
                return MapToDto(artist).ToSuccess();
            }

            var imageBytes = await response.Content.ReadAsByteArrayAsync();

            var fileExtension = Path.GetExtension(imageUrl).Split('?')[0];

            if (string.IsNullOrWhiteSpace(fileExtension) || fileExtension.Length > 5)
            {
                switch (contentType)
                {
                    case "image/jpeg": fileExtension = ".jpg"; break;
                    case "image/png": fileExtension = ".png"; break;
                    case "image/gif": fileExtension = ".gif"; break;
                    case "image/webp": fileExtension = ".webp"; break;
                    default: fileExtension = ".jpg"; break;
                }
            }

            var fileName = $"{artist.Slug}-{Guid.NewGuid()}{fileExtension}";
            var savedFileName = await imageService.SaveImageAsync(fileName, imageBytes);

            artist.ImageUrl = savedFileName;
            await repository.UpdateAsync(artist);
        }
        catch (Exception)
        {
        }

        return MapToDto(artist).ToSuccess();
    }

    public async Task<Result<int>> EnrichAllArtistsAsync()
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
            }
        }

        return count.ToSuccess();
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
