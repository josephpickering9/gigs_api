using Microsoft.EntityFrameworkCore;
using Gigs.Models;
using Gigs.Services;

namespace Gigs.Repositories;

public class ArtistRepository(Database database) : IArtistRepository
{
    public async Task<List<Artist>> GetAllAsync()
    {
        return await database.Artist
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task UpdateAsync(Artist artist)
    {
        database.Artist.Update(artist);
        await database.SaveChangesAsync();
    }
}
