using Gigs.DataModels;
using Gigs.Models;
using Gigs.Repositories;
using Gigs.Services.AI;
using Gigs.Services.Image;
using Gigs.Types;

namespace Gigs.Services;

public class VenueService(
    VenueRepository repository,
    AiEnrichmentService aiEnrichmentService,
    ImageService imageService,
    IHttpClientFactory httpClientFactory)
{
    public async Task<Result<List<GetVenueResponse>>> GetAllAsync()
    {
        var venues = await repository.GetAllAsync();
        return venues.Select(MapToDto).ToList().ToSuccess();
    }

    public async Task<Result<GetVenueResponse>> EnrichVenueAsync(VenueId id)
    {
        var venues = await repository.GetAllAsync();
        var venue = venues.FirstOrDefault(v => v.Id == id);

        if (venue == null)
        {
            return Result.NotFound<GetVenueResponse>($"Venue with ID {id} not found.");
        }

        var aiResult = await aiEnrichmentService.EnrichVenueImage(venue.Name, venue.City);
        var imageUrl = aiResult.IsSuccess ? aiResult.Data : null;

        if (string.IsNullOrWhiteSpace(imageUrl))
            return MapToDto(venue).ToSuccess();

        try
        {
            var client = httpClientFactory.CreateClient();
            var imageBytes = await client.GetByteArrayAsync(imageUrl);

            var fileExtension = Path.GetExtension(imageUrl).Split('?')[0];
            if (string.IsNullOrWhiteSpace(fileExtension) || fileExtension.Length > 5)
                fileExtension = ".jpg";

            var fileName = $"{venue.Slug}-{Guid.NewGuid()}{fileExtension}";
            var savedFileName = await imageService.SaveImageAsync(fileName, imageBytes);

            venue.ImageUrl = savedFileName;
            await repository.UpdateAsync(venue);
        }
        catch (Exception)
        {
        }

        return MapToDto(venue).ToSuccess();
    }

    public async Task<Result<int>> EnrichAllVenuesAsync()
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
            }
        }

        return count.ToSuccess();
    }

    private static GetVenueResponse MapToDto(Venue venue)
    {
        return new GetVenueResponse
        {
            Id = venue.Id,
            Name = venue.Name,
            City = venue.City,
            ImageUrl = venue.ImageUrl,
            Slug = venue.Slug,
            GigCount = venue.Gigs.Count
        };
    }
}
