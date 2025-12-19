using Gigs.DTOs;

namespace Gigs.Services;

public interface IDashboardService
{
    Task<DashboardStatsResponse> GetDashboardStatsAsync();
}
