using Microsoft.EntityFrameworkCore;
using Gigs.Services;

namespace Gigs.Repositories;

public class DashboardRepository(Database database) : IDashboardRepository
{
    public async Task<int> GetTotalGigsCountAsync()
    {
        return await database.Gig.CountAsync();
    }
}
