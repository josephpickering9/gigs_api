using Microsoft.EntityFrameworkCore;
using Gigs.DTOs;
using Gigs.Services;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Repositories;

public class DashboardRepository(Database database)
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
                GigCount = g.Count(),
            })
            .OrderByDescending(x => x.GigCount)
            .FirstOrDefaultAsync();

        var topVenue = await database.Gig
            .Include(g => g.Venue)
            .GroupBy(g => new { g.VenueId, g.Venue.Name, g.Date })
            .Select(g => new { g.Key.VenueId, g.Key.Name, g.Key.Date })
            .GroupBy(x => new { x.VenueId, x.Name })
            .Select(g => new TopVenueStats
            {
                VenueName = g.Key.Name,
                GigCount = g.Count(), // Count of unique venue-date pairs
            })
            .OrderByDescending(x => x.GigCount)
            .FirstOrDefaultAsync();

        var topCity = await database.Gig
            .Include(g => g.Venue)
            .GroupBy(g => new { g.Venue.City, g.Date })
            .Select(g => new { g.Key.City, g.Key.Date })
            .GroupBy(x => new { x.City })
            .Select(g => new TopCityStats
            {
                CityName = g.Key.City,
                GigCount = g.Count(), // Count of unique city-date pairs
            })
            .OrderByDescending(x => x.GigCount)
            .FirstOrDefaultAsync();

        var topAttendee = await database.GigAttendee
            .Include(ga => ga.Person)
            .GroupBy(ga => new { ga.PersonId, ga.Person.Name })
            .Select(g => new TopAttendeeStats
            {
                PersonName = g.Key.Name,
                GigCount = g.Count(),
            })
            .OrderByDescending(x => x.GigCount)
            .FirstOrDefaultAsync();

        return new DashboardStatsResponse
        {
            TotalGigs = totalGigs,
            TopArtist = topArtist,
            TopVenue = topVenue,
            TopCity = topCity,
            TopAttendee = topAttendee,
        };
    }

    public async Task<List<AverageTicketPriceByYearResponse>> GetAverageTicketPriceByYearAsync()
    {
        return await database.Gig
            .Where(g => g.TicketCost.HasValue)
            .GroupBy(g => g.Date.Year)
            .Select(g => new AverageTicketPriceByYearResponse
            {
                Year = g.Key,
                AveragePrice = g.Average(x => x.TicketCost!.Value),
            })
            .OrderBy(x => x.Year)
            .ToListAsync();
    }

    public async Task<List<GigsPerYearResponse>> GetGigsPerYearAsync()
    {
        return await database.Gig
            .GroupBy(g => g.Date.Year)
            .Select(g => new GigsPerYearResponse
            {
                Year = g.Key,
                GigCount = g.Count(),
            })
            .OrderBy(x => x.Year)
            .ToListAsync();
    }

    public async Task<List<GigsPerMonthResponse>> GetGigsPerMonthAsync()
    {
        var monthNames = new[]
        {
            string.Empty, "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December",
        };

        return await database.Gig
            .GroupBy(g => g.Date.Month)
            .Select(g => new GigsPerMonthResponse
            {
                Month = g.Key,
                MonthName = monthNames[g.Key],
                GigCount = g.Count(),
            })
            .OrderBy(x => x.Month)
            .ToListAsync();
    }

    public async Task<TemporalStatsResponse> GetTemporalStatsAsync()
    {
        var busiestYear = await database.Gig
            .GroupBy(g => g.Date.Year)
            .Select(g => new { Year = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .FirstOrDefaultAsync();

        var lastGigDate = await database.Gig
            .OrderByDescending(g => g.Date)
            .Select(g => g.Date)
            .FirstOrDefaultAsync();

        int? daysSinceLastGig = null;
        if (lastGigDate != default)
        {
            daysSinceLastGig = (DateOnly.FromDateTime(DateTime.Now).ToDateTime(TimeOnly.MinValue) - lastGigDate.ToDateTime(TimeOnly.MinValue)).Days;
        }

        return new TemporalStatsResponse
        {
            BusiestYear = busiestYear?.Year,
            BusiestYearGigCount = busiestYear?.Count,
            DaysSinceLastGig = daysSinceLastGig,
        };
    }

    public async Task<ArtistInsightsResponse> GetArtistInsightsAsync()
    {
        var totalUniqueArtists = await database.GigArtist
            .Select(ga => ga.ArtistId)
            .Distinct()
            .CountAsync();

        var totalAppearances = await database.GigArtist.CountAsync();

        return new ArtistInsightsResponse
        {
            TotalUniqueArtists = totalUniqueArtists,
            TotalArtistAppearances = totalAppearances,
        };
    }

    public async Task<List<TopArtistResponse>> GetTopArtistsAsync(int limit = 10)
    {
        return await database.GigArtist
            .Include(ga => ga.Artist)
            .GroupBy(ga => new { ga.ArtistId, ga.Artist.Name })
            .Select(g => new TopArtistResponse
            {
                ArtistId = g.Key.ArtistId.ToString(),
                ArtistName = g.Key.Name,
                TotalAppearances = g.Count(),
                AsHeadliner = g.Count(x => x.IsHeadliner),
                AsSupport = g.Count(x => !x.IsHeadliner),
            })
            .OrderByDescending(x => x.TotalAppearances)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<VenueInsightsResponse> GetVenueInsightsAsync()
    {
        var totalUniqueVenues = await database.Gig
            .Select(g => g.VenueId)
            .Distinct()
            .CountAsync();

        var totalUniqueCities = await database.Gig
            .Include(g => g.Venue)
            .Select(g => g.Venue.City)
            .Distinct()
            .CountAsync();

        return new VenueInsightsResponse
        {
            TotalUniqueVenues = totalUniqueVenues,
            TotalUniqueCities = totalUniqueCities,
        };
    }

    public async Task<List<TopVenueResponse>> GetTopVenuesAsync(int limit = 10)
    {
        return await database.Gig
            .Include(g => g.Venue)
            .GroupBy(g => new { g.VenueId, g.Venue.Name, g.Venue.City, g.Date })
            .Select(g => new { g.Key.VenueId, g.Key.Name, g.Key.City, g.Key.Date })
            .GroupBy(x => new { x.VenueId, x.Name, x.City })
            .Select(g => new TopVenueResponse
            {
                VenueId = g.Key.VenueId.ToString(),
                VenueName = g.Key.Name,
                City = g.Key.City,
                GigCount = g.Count(),
            })
            .OrderByDescending(x => x.GigCount)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<TopCityResponse>> GetTopCitiesAsync(int limit = 10)
    {
        return await database.Gig
            .Include(g => g.Venue)
            .GroupBy(g => new { g.Venue.City, g.Date })
            .Select(g => new { g.Key.City, g.Key.Date, VenueId = g.First().VenueId })
            .GroupBy(x => x.City)
            .Select(g => new TopCityResponse
            {
                City = g.Key,
                GigCount = g.Count(),
                UniqueVenues = g.Select(x => x.VenueId).Distinct().Count(),
            })
            .OrderByDescending(x => x.GigCount)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<InterestingInsightsResponse> GetInterestingInsightsAsync()
    {
        // Find longest setlist
        var longestSetlist = await database.GigArtist
            .Include(ga => ga.Songs)
            .ThenInclude(gas => gas.Song)
            .Include(ga => ga.Artist)
            .Include(ga => ga.Gig)
            .ThenInclude(g => g.Venue)
            .Where(ga => ga.Songs.Any())
            .OrderByDescending(ga => ga.Songs.Count)
            .Select(ga => new LongestSetlistInfo
            {
                GigId = ga.GigId.ToString(),
                ArtistName = ga.Artist.Name,
                VenueName = ga.Gig.Venue.Name,
                Date = ga.Gig.Date,
                SongCount = ga.Songs.Count,
            })
            .FirstOrDefaultAsync();

        // Calculate gig streak (consecutive months with gigs)
        var gigsByMonth = await database.Gig
            .OrderBy(g => g.Date)
            .Select(g => new { Year = g.Date.Year, Month = g.Date.Month })
            .Distinct()
            .ToListAsync();

        int longestStreak = 0;
        int currentStreak = 0;
        DateTime? lastMonth = null;

        foreach (var month in gigsByMonth)
        {
            var currentMonth = new DateTime(month.Year, month.Month, 1);

            if (lastMonth == null || currentMonth == lastMonth.Value.AddMonths(1))
            {
                currentStreak++;
                longestStreak = Math.Max(longestStreak, currentStreak);
            }
            else
            {
                currentStreak = 1;
            }

            lastMonth = currentMonth;
        }

        // Calculate average gigs per year
        var firstGig = await database.Gig.OrderBy(g => g.Date).Select(g => g.Date).FirstOrDefaultAsync();
        var lastGig = await database.Gig.OrderByDescending(g => g.Date).Select(g => g.Date).FirstOrDefaultAsync();
        var totalGigs = await database.Gig.CountAsync();

        decimal avgGigsPerYear = 0;
        if (firstGig != default && lastGig != default && totalGigs > 0)
        {
            var yearSpan = lastGig.Year - firstGig.Year + 1;
            avgGigsPerYear = yearSpan > 0 ? (decimal)totalGigs / yearSpan : totalGigs;
        }

        return new InterestingInsightsResponse
        {
            LongestSetlist = longestSetlist,
            LongestGigStreak = longestStreak > 0 ? longestStreak : null,
            AverageGigsPerYear = Math.Round(avgGigsPerYear, 1),
        };
    }

    public async Task<List<MostHeardSongResponse>> GetMostHeardSongsAsync(int limit = 10)
    {
        return await database.GigArtistSong
            .Include(gas => gas.Song)
            .ThenInclude(s => s.Artist)
            .GroupBy(gas => new { gas.SongId, gas.Song.Title, gas.Song.Artist.Name })
            .Select(g => new MostHeardSongResponse
            {
                SongId = g.Key.SongId.ToString(),
                SongTitle = g.Key.Title,
                ArtistName = g.Key.Name,
                TimesHeard = g.Count(),
            })
            .OrderByDescending(x => x.TimesHeard)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<AttendeeInsightsResponse> GetAttendeeInsightsAsync()
    {
        var totalUniqueAttendees = await database.GigAttendee
            .Select(ga => ga.PersonId)
            .Distinct()
            .CountAsync();

        var totalGigsWithAttendees = await database.Gig
            .Where(g => g.Attendees.Any())
            .CountAsync();

        return new AttendeeInsightsResponse
        {
            TotalUniqueAttendees = totalUniqueAttendees,
            TotalGigsWithAttendees = totalGigsWithAttendees,
        };
    }

    public async Task<List<TopAttendeeResponse>> GetTopAttendeesAsync(int limit = 10)
    {
        return await database.GigAttendee
            .Include(ga => ga.Person)
            .GroupBy(ga => new { ga.PersonId, ga.Person.Name })
            .Select(g => new TopAttendeeResponse
            {
                PersonId = g.Key.PersonId.ToString(),
                PersonName = g.Key.Name,
                GigCount = g.Count(),
            })
            .OrderByDescending(x => x.GigCount)
            .Take(limit)
            .ToListAsync();
    }
}
