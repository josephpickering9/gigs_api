using Gigs.DataModels;
using Microsoft.EntityFrameworkCore;
using Gigs.Models;
using Gigs.Services;
using Gigs.Types;

namespace Gigs.Repositories;

public class ArtistRepository(Database database)
{
    public async Task<List<Artist>> GetAllAsync(GigFilterCriteria? filter = null)
    {
        var artists = await database.Artist
            .Include(a => a.Gigs)
                .ThenInclude(ga => ga.Gig)
                    .ThenInclude(g => g.Venue)
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync();

        // Apply filters in memory after materialization to avoid EF Core circular reference issues
        if (filter != null && HasAnyFilter(filter))
        {
            return artists
                .Where(a => a.Gigs.Any(ga => MatchesFilter(ga.Gig, filter)))
                .ToList();
        }

        return artists;
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

        // Skip ArtistId filter for artist endpoint to avoid circular reference
        // Skip AttendeeId filter to avoid loading circular references

        return true;
    }

    public async Task UpdateAsync(Artist artist)
    {
        database.Artist.Update(artist);
        await database.SaveChangesAsync();
    }

    public async Task<ArtistId> GetOrCreateAsync(string name)
    {
        var artist = database.Artist.Local.FirstOrDefault(a => a.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                     ?? await database.Artist.FirstOrDefaultAsync(a => a.Name.ToLower() == name.ToLower());

        if (artist == null)
        {
            artist = new Artist
            {
                Name = name,
                Slug = Guid.NewGuid().ToString(),
            };
            database.Artist.Add(artist);
        }

        return artist.Id;
    }
}
