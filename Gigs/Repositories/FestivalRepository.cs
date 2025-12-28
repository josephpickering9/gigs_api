using Gigs.Models;
using Gigs.Services;
using Gigs.Types;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Repositories;

public class FestivalRepository(Database database)
{
    public async Task<List<Festival>> GetAllAsync()
    {
        return await database.Festival
            .Include(f => f.Gigs)
                .ThenInclude(g => g.Venue)
            .Include(f => f.Gigs)
                .ThenInclude(g => g.Acts)
                    .ThenInclude(a => a.Artist)
            .Include(f => f.Gigs)
                .ThenInclude(g => g.Acts)
                    .ThenInclude(a => a.Songs)
                        .ThenInclude(s => s.Song)
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    public async Task<Festival?> GetByIdAsync(FestivalId id)
    {
        return await database.Festival
            .Include(f => f.Gigs)
                .ThenInclude(g => g.Venue)
            .Include(f => f.Gigs)
                .ThenInclude(g => g.Acts)
                    .ThenInclude(a => a.Artist)
            .Include(f => f.Gigs)
                .ThenInclude(g => g.Acts)
                    .ThenInclude(a => a.Songs)
                        .ThenInclude(s => s.Song)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task AddAsync(Festival festival)
    {
        database.Festival.Add(festival);
        await database.SaveChangesAsync();
    }

    public async Task<FestivalId> GetOrCreateAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Festival name cannot be empty.");
        }

        var festival = database.Festival.Local.FirstOrDefault(f => f.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                       ?? await database.Festival.FirstOrDefaultAsync(f => f.Name.ToLower() == name.ToLower());

        if (festival == null)
        {
            festival = new Festival
            {
                Name = name,
                Slug = Guid.NewGuid().ToString(),
            };
            database.Festival.Add(festival);
        }

        return festival.Id;
    }

    public async Task<Festival> UpdateAsync(Festival festival)
    {
        database.Festival.Update(festival);
        await database.SaveChangesAsync();
        return festival;
    }

    public async Task DeleteAsync(FestivalId id)
    {
        var festival = await database.Festival.FindAsync(id);
        if (festival != null)
        {
            database.Festival.Remove(festival);
            await database.SaveChangesAsync();
        }
    }

    public async Task<Festival?> FindByNameAsync(string name)
    {
        return database.Festival.Local.FirstOrDefault(f => f.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
               ?? await database.Festival
                   .Include(f => f.Gigs)
                       .ThenInclude(g => g.Venue)
                   .Include(f => f.Gigs)
                       .ThenInclude(g => g.Acts)
                           .ThenInclude(a => a.Artist)
                   .Include(f => f.Gigs)
                       .ThenInclude(g => g.Acts)
                           .ThenInclude(a => a.Songs)
                               .ThenInclude(s => s.Song)
                   .FirstOrDefaultAsync(f => f.Name.ToLower() == name.ToLower());
    }
}
