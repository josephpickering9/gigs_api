using Gigs.Models;

namespace Gigs.Repositories;

public interface IArtistRepository
{
    Task<List<Artist>> GetAllAsync();
    Task UpdateAsync(Artist artist);
}
