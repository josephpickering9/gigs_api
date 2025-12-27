using Gigs.DTOs;

namespace Gigs.Repositories;

public interface IDashboardRepository
{
    Task<DashboardStatsResponse> GetDashboardStatsAsync();
    Task<List<AverageTicketPriceByYearResponse>> GetAverageTicketPriceByYearAsync();
    Task<List<GigsPerYearResponse>> GetGigsPerYearAsync();
    Task<List<GigsPerMonthResponse>> GetGigsPerMonthAsync();
    Task<TemporalStatsResponse> GetTemporalStatsAsync();
    Task<ArtistInsightsResponse> GetArtistInsightsAsync();
    Task<List<TopArtistResponse>> GetTopArtistsAsync(int limit = 10);
    Task<VenueInsightsResponse> GetVenueInsightsAsync();
    Task<List<TopVenueResponse>> GetTopVenuesAsync(int limit = 10);
    Task<List<TopCityResponse>> GetTopCitiesAsync(int limit = 10);
    Task<InterestingInsightsResponse> GetInterestingInsightsAsync();
    Task<List<MostHeardSongResponse>> GetMostHeardSongsAsync(int limit = 10);
    Task<AttendeeInsightsResponse> GetAttendeeInsightsAsync();
    Task<List<TopAttendeeResponse>> GetTopAttendeesAsync(int limit = 10);
}
