using Microsoft.EntityFrameworkCore;
using Gigs.Models;
using Gigs.Types;
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

    public async Task<VenueId> GetOrCreateAsync(string name, string city)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(city))
        {
            throw new ArgumentException("Both VenueName and VenueCity must be provided.");
        }

        var venue = database.Venue.Local.FirstOrDefault(v => v.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase) && v.City.Equals(city, StringComparison.CurrentCultureIgnoreCase))
                    ?? await database.Venue.FirstOrDefaultAsync(v => v.Name.ToLower() == name.ToLower() && v.City.ToLower() == city.ToLower());

        if (venue == null)
        {
            venue = new Venue
            {
                Name = name,
                City = city,
                Slug = Guid.NewGuid().ToString()
            };
            database.Venue.Add(venue);
            await database.SaveChangesAsync();
        }

        return venue.Id;
    }
}
