using Gigs.Models;
using Gigs.Types;

namespace Gigs.Repositories;

public interface IFestivalRepository
{
    Task<List<Festival>> GetAllAsync();
    Task<Festival?> GetByIdAsync(FestivalId id);
    Task<Festival> AddAsync(Festival festival);
    Task<Festival> UpdateAsync(Festival festival);
    Task DeleteAsync(FestivalId id);
    Task<Festival?> FindByNameAsync(string name);
}
