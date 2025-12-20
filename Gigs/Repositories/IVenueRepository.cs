using Gigs.Models;

namespace Gigs.Repositories;

public interface IVenueRepository
{
    Task<List<Venue>> GetAllAsync();
    Task UpdateAsync(Venue venue);
}
