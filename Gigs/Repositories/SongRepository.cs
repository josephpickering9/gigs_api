using Microsoft.EntityFrameworkCore;
using Gigs.Models;
using Gigs.Types;
using Gigs.Services;

namespace Gigs.Repositories;

public class SongRepository(Database database)
{
    public async Task<Song> GetOrCreateAsync(ArtistId artistId, string title)
    {
        var song = database.Song.Local.FirstOrDefault(s => s.ArtistId == artistId && s.Title.Equals(title, StringComparison.CurrentCultureIgnoreCase))
                   ?? await database.Song.FirstOrDefaultAsync(s => s.ArtistId == artistId && s.Title.ToLower() == title.ToLower());

        if (song == null)
        {
            song = new Song
            {
                ArtistId = artistId,
                Title = title,
                Slug = Guid.NewGuid().ToString()
            };
            database.Song.Add(song);
            await database.SaveChangesAsync();
        }

        return song;
    }
}
