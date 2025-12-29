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
    public async Task<Result<List<GetVenueResponse>>> GetAllAsync(GigFilterCriteria? filter = null)
    {
        var venues = await repository.GetAllAsync(filter);
        return venues.Select(v => MapToDto(v, filter)).ToList().ToSuccess();
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

    private static GetVenueResponse MapToDto(Venue venue, GigFilterCriteria? filter = null)
    {
        var gigCount = venue.Gigs.Count;

        // If filters are provided, count only gigs that match the criteria
        if (filter != null && HasAnyFilter(filter))
        {
            gigCount = venue.Gigs.Count(g => MatchesFilter(g, filter));
        }

        return new GetVenueResponse
        {
            Id = venue.Id,
            Name = venue.Name,
            City = venue.City,
            ImageUrl = venue.ImageUrl,
            Slug = venue.Slug,
            GigCount = gigCount
        };
    }

    private static bool HasAnyFilter(GigFilterCriteria filter)
    {
        return filter.VenueId.HasValue
            || filter.FestivalId.HasValue
            || !string.IsNullOrWhiteSpace(filter.City)
            || filter.FromDate.HasValue
            || filter.ToDate.HasValue
            || filter.ArtistId.HasValue
            || filter.AttendeeId.HasValue;
    }

    private static bool MatchesFilter(Gig gig, GigFilterCriteria filter)
    {
        if (filter.VenueId.HasValue && gig.VenueId != filter.VenueId.Value)
            return false;

        if (filter.FestivalId.HasValue && gig.FestivalId != filter.FestivalId.Value)
            return false;

        if (!string.IsNullOrWhiteSpace(filter.City) && gig.Venue != null &&
            !gig.Venue.City.Equals(filter.City, StringComparison.OrdinalIgnoreCase))
            return false;

        if (filter.FromDate.HasValue && gig.Date < filter.FromDate.Value)
            return false;

        if (filter.ToDate.HasValue && gig.Date > filter.ToDate.Value)
            return false;

        if (filter.ArtistId.HasValue && !gig.Acts.Any(a => a.ArtistId == filter.ArtistId.Value))
            return false;

        if (filter.AttendeeId.HasValue && !gig.Attendees.Any(a => a.PersonId == filter.AttendeeId.Value))
            return false;

        return true;
    }
}
