using Gigs.Models;
using Gigs.Types;

namespace Gigs.Repositories;

public interface ISongRepository
{
    Task<Song> GetOrCreateAsync(ArtistId artistId, string title);
}
