using Gigs.Repositories;

namespace Gigs.Services;

public class DashboardService(IDashboardRepository dashboardRepository) : IDashboardService
{
    public async Task<int> GetTotalGigsCountAsync()
    {
        return await dashboardRepository.GetTotalGigsCountAsync();
    }
}
