namespace Gigs.Repositories;

public interface IDashboardRepository
{
    Task<int> GetTotalGigsCountAsync();
}
