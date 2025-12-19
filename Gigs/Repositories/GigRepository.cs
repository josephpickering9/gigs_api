using Microsoft.EntityFrameworkCore;
using Gigs.Exceptions;
using Gigs.Models;
using Gigs.Services;
using Gigs.Types;

namespace Gigs.Repositories;

public class GigRepository(Database database) : IGigRepository
{
    public async Task<List<Gig>> GetAllAsync()
    {
        return await database.Gig
            .Include(g => g.Venue)
            .Include(g => g.Acts).ThenInclude(ga => ga.Artist)
            .Include(g => g.Attendees) // Assuming we might want count or list later
            .AsNoTracking() // Read-only for list
            .ToListAsync();
    }

    public async Task<Gig?> GetByIdAsync(GigId id)
    {
        return await database.Gig
            .Include(g => g.Venue)
            .Include(g => g.Acts).ThenInclude(ga => ga.Artist)
            .Include(g => g.Attendees)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<Gig> AddAsync(Gig gig)
    {
        await database.Gig.AddAsync(gig);
        await database.SaveChangesAsync();
        return gig;
    }

    public async Task<Gig> UpdateAsync(Gig gig)
    {
        database.Gig.Update(gig);
        await database.SaveChangesAsync();
        return gig;
    }

    public async Task DeleteAsync(GigId id)
    {
        var gig = await database.Gig.FindAsync(id);
        if (gig != null)
        {
            database.Gig.Remove(gig);
            await database.SaveChangesAsync();
        }
    }
}
