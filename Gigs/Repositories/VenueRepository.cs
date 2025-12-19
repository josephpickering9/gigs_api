using Microsoft.EntityFrameworkCore;
using Gigs.Models;
using Gigs.Services;

namespace Gigs.Repositories;

public class VenueRepository(Database database) : IVenueRepository
{
    public async Task<List<Venue>> GetAllAsync()
    {
        return await database.Venue
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .ToListAsync();
    }

    public async Task UpdateAsync(Venue venue)
    {
        database.Venue.Update(venue);
        await database.SaveChangesAsync();
    }
}
