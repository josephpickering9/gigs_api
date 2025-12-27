using Gigs.DTOs;
using Gigs.Repositories;
using Gigs.Types;

namespace Gigs.Services;

public class DashboardService(DashboardRepository dashboardRepository)
{
    public async Task<Result<DashboardStatsResponse>> GetDashboardStatsAsync()
    {
        return (await dashboardRepository.GetDashboardStatsAsync()).ToSuccess();
    }

    public async Task<Result<List<AverageTicketPriceByYearResponse>>> GetAverageTicketPriceByYearAsync()
    {
        return (await dashboardRepository.GetAverageTicketPriceByYearAsync()).ToSuccess();
    }

    public async Task<Result<List<GigsPerYearResponse>>> GetGigsPerYearAsync()
    {
        return (await dashboardRepository.GetGigsPerYearAsync()).ToSuccess();
    }

    public async Task<Result<List<GigsPerMonthResponse>>> GetGigsPerMonthAsync()
    {
        return (await dashboardRepository.GetGigsPerMonthAsync()).ToSuccess();
    }

    public async Task<Result<TemporalStatsResponse>> GetTemporalStatsAsync()
    {
        return (await dashboardRepository.GetTemporalStatsAsync()).ToSuccess();
    }

    public async Task<Result<ArtistInsightsResponse>> GetArtistInsightsAsync()
    {
        return (await dashboardRepository.GetArtistInsightsAsync()).ToSuccess();
    }

    public async Task<Result<List<TopArtistResponse>>> GetTopArtistsAsync(int limit = 10)
    {
        return (await dashboardRepository.GetTopArtistsAsync(limit)).ToSuccess();
    }

    public async Task<Result<VenueInsightsResponse>> GetVenueInsightsAsync()
    {
        return (await dashboardRepository.GetVenueInsightsAsync()).ToSuccess();
    }

    public async Task<Result<List<TopVenueResponse>>> GetTopVenuesAsync(int limit = 10)
    {
        return (await dashboardRepository.GetTopVenuesAsync(limit)).ToSuccess();
    }

    public async Task<Result<List<TopCityResponse>>> GetTopCitiesAsync(int limit = 10)
    {
        return (await dashboardRepository.GetTopCitiesAsync(limit)).ToSuccess();
    }

    public async Task<Result<InterestingInsightsResponse>> GetInterestingInsightsAsync()
    {
        return (await dashboardRepository.GetInterestingInsightsAsync()).ToSuccess();
    }

    public async Task<Result<List<MostHeardSongResponse>>> GetMostHeardSongsAsync(int limit = 10)
    {
        return (await dashboardRepository.GetMostHeardSongsAsync(limit)).ToSuccess();
    }

    public async Task<Result<AttendeeInsightsResponse>> GetAttendeeInsightsAsync()
    {
        return (await dashboardRepository.GetAttendeeInsightsAsync()).ToSuccess();
    }

    public async Task<Result<List<TopAttendeeResponse>>> GetTopAttendeesAsync(int limit = 10)
    {
        return (await dashboardRepository.GetTopAttendeesAsync(limit)).ToSuccess();
    }
}
