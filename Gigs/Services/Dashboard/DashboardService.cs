using Gigs.DTOs;
using Gigs.Repositories;

namespace Gigs.Services;

public class DashboardService(IDashboardRepository dashboardRepository) : IDashboardService
{
    public async Task<DashboardStatsResponse> GetDashboardStatsAsync()
    {
        return await dashboardRepository.GetDashboardStatsAsync();
    }
}
