namespace Gigs.Services;

public interface IDashboardService
{
    Task<int> GetTotalGigsCountAsync();
}
