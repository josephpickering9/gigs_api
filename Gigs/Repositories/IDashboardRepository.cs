using Gigs.DTOs;

namespace Gigs.Repositories;

public interface IDashboardRepository
{
    Task<DashboardStatsResponse> GetDashboardStatsAsync();
}
