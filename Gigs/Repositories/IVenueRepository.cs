using Gigs.Models;
using Gigs.Types;

namespace Gigs.Repositories;

public interface IVenueRepository
{
    Task<List<Venue>> GetAllAsync();
    Task UpdateAsync(Venue venue);
    Task<VenueId> GetOrCreateAsync(string name, string city);
}
