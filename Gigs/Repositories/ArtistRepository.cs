using Microsoft.EntityFrameworkCore;
using Gigs.Models;
using Gigs.Services;
using Gigs.Types;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Repositories;

public class ArtistRepository(Database database)
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

    public async Task<ArtistId> GetOrCreateAsync(string name)
    {
        var artist = database.Artist.Local.FirstOrDefault(a => a.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                     ?? await database.Artist.FirstOrDefaultAsync(a => a.Name.ToLower() == name.ToLower());

        if (artist == null)
        {
            artist = new Artist
            {
                Name = name,
                Slug = Guid.NewGuid().ToString(),
            };
            database.Artist.Add(artist);
        }

        return artist.Id;
    }
}
