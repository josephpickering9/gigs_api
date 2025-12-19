using Microsoft.EntityFrameworkCore;
using Gigs.DTOs;
using Gigs.Services;

namespace Gigs.Repositories;

public class DashboardRepository(Database database) : IDashboardRepository
{
    public async Task<DashboardStatsResponse> GetDashboardStatsAsync()
    {
        var totalGigs = await database.Gig.CountAsync();

        var topArtist = await database.Gig
            .Include(g => g.Acts)
            .ThenInclude(ga => ga.Artist)
            .SelectMany(g => g.Acts)
            .GroupBy(ga => new { ga.Artist.Name })
            .Select(g => new TopArtistStats
            {
                ArtistName = g.Key.Name,
                GigCount = g.Count()
            })
            .OrderByDescending(x => x.GigCount)
            .FirstOrDefaultAsync();

        var topVenue = await database.Gig
            .Include(g => g.Venue)
            .GroupBy(g => new { g.Venue.Name })
            .Select(g => new TopVenueStats
            {
                VenueName = g.Key.Name,
                GigCount = g.Count()
            })
            .OrderByDescending(x => x.GigCount)
            .FirstOrDefaultAsync();

        var topCity = await database.Gig
            .Include(g => g.Venue)
            .GroupBy(g => new { g.Venue.City })
            .Select(g => new TopCityStats
            {
                CityName = g.Key.City,
                GigCount = g.Count()
            })
            .OrderByDescending(x => x.GigCount)
            .FirstOrDefaultAsync();

        return new DashboardStatsResponse
        {
            TotalGigs = totalGigs,
            TopArtist = topArtist,
            TopVenue = topVenue,
            TopCity = topCity
        };
    }
}
