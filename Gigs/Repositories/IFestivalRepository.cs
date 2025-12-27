using Gigs.Models;
using Gigs.Types;

namespace Gigs.Repositories;

public interface IFestivalRepository
{
    Task<List<Festival>> GetAllAsync();
    Task<Festival?> GetByIdAsync(FestivalId id);
    Task<Festival?> FindByNameAsync(string name);
    Task AddAsync(Festival festival);
    Task<Festival> UpdateAsync(Festival festival);
    Task<FestivalId> GetOrCreateAsync(string name);
    Task DeleteAsync(FestivalId id);
}
