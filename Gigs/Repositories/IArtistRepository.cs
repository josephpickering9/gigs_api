using Gigs.Models;
using Gigs.Types;

namespace Gigs.Repositories;

public interface IArtistRepository
{
    Task<List<Artist>> GetAllAsync();
    Task UpdateAsync(Artist artist);
    Task<ArtistId> GetOrCreateAsync(string name);
}
