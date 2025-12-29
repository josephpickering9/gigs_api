using Gigs.DataModels;
using Microsoft.EntityFrameworkCore;
using Gigs.Models;
using Gigs.Services;
using Gigs.Types;

namespace Gigs.Repositories;

public class VenueRepository(Database database)
{
    public async Task<List<Venue>> GetAllAsync(GigFilterCriteria? filter = null)
    {
        var venues = await database.Venue
            .Include(v => v.Gigs)
                .ThenInclude(g => g.Acts)
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .ToListAsync();

        // Apply filters in memory after materialization to avoid EF Core circular reference issues
        if (filter != null && HasAnyFilter(filter))
        {
            return venues
                .Where(v => v.Gigs.Any(g => MatchesFilter(g, filter)))
                .ToList();
        }

        return venues;
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

        // Skip AttendeeId filter to avoid loading circular references

        return true;
    }

    public async Task UpdateAsync(Venue venue)
    {
        database.Venue.Update(venue);
        await database.SaveChangesAsync();
    }

    public async Task<VenueId> GetOrCreateAsync(string name, string city)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("Both VenueName and VenueCity must be provided.");
        }

        var venue = database.Venue.Local.FirstOrDefault(v => v.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase) && v.City.Equals(city, StringComparison.CurrentCultureIgnoreCase))
                    ?? await database.Venue.FirstOrDefaultAsync(v => v.Name.ToLower() == name.ToLower() && v.City.ToLower() == city.ToLower());

        if (venue == null)
        {
            venue = new Venue
            {
                Name = name,
                City = city,
                Slug = Guid.NewGuid().ToString(),
            };
            database.Venue.Add(venue);
        }

        return venue.Id;
    }
}
