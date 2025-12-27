using Gigs.DTOs;
using Gigs.Repositories;

namespace Gigs.Services;

public class DashboardService(DashboardRepository dashboardRepository)
{
    public async Task<DashboardStatsResponse> GetDashboardStatsAsync()
    {
        return await dashboardRepository.GetDashboardStatsAsync();
    }

    public async Task<List<AverageTicketPriceByYearResponse>> GetAverageTicketPriceByYearAsync()
    {
        return await dashboardRepository.GetAverageTicketPriceByYearAsync();
    }

    public async Task<List<GigsPerYearResponse>> GetGigsPerYearAsync()
    {
        return await dashboardRepository.GetGigsPerYearAsync();
    }

    public async Task<List<GigsPerMonthResponse>> GetGigsPerMonthAsync()
    {
        return await dashboardRepository.GetGigsPerMonthAsync();
    }

    public async Task<TemporalStatsResponse> GetTemporalStatsAsync()
    {
        return await dashboardRepository.GetTemporalStatsAsync();
    }

    public async Task<ArtistInsightsResponse> GetArtistInsightsAsync()
    {
        return await dashboardRepository.GetArtistInsightsAsync();
    }

    public async Task<List<TopArtistResponse>> GetTopArtistsAsync(int limit = 10)
    {
        return await dashboardRepository.GetTopArtistsAsync(limit);
    }

    public async Task<VenueInsightsResponse> GetVenueInsightsAsync()
    {
        return await dashboardRepository.GetVenueInsightsAsync();
    }

    public async Task<List<TopVenueResponse>> GetTopVenuesAsync(int limit = 10)
    {
        return await dashboardRepository.GetTopVenuesAsync(limit);
    }

    public async Task<List<TopCityResponse>> GetTopCitiesAsync(int limit = 10)
    {
        return await dashboardRepository.GetTopCitiesAsync(limit);
    }

    public async Task<InterestingInsightsResponse> GetInterestingInsightsAsync()
    {
        return await dashboardRepository.GetInterestingInsightsAsync();
    }

    public async Task<List<MostHeardSongResponse>> GetMostHeardSongsAsync(int limit = 10)
    {
        return await dashboardRepository.GetMostHeardSongsAsync(limit);
    }

    public async Task<AttendeeInsightsResponse> GetAttendeeInsightsAsync()
    {
        return await dashboardRepository.GetAttendeeInsightsAsync();
    }

    public async Task<List<TopAttendeeResponse>> GetTopAttendeesAsync(int limit = 10)
    {
        return await dashboardRepository.GetTopAttendeesAsync(limit);
    }
}
