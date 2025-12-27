using Gigs.DTOs;
using Gigs.Models;
using Gigs.Types;

namespace Gigs.Repositories;

public interface IGigRepository
{
    Task<(List<Gig> Items, int TotalCount)> GetAllAsync(GetGigsFilter filter);
    Task<Gig?> GetByIdAsync(GigId id);
    Task<Gig> AddAsync(Gig gig);
    Task<Gig?> FindAsync(VenueId venueId, DateOnly date, ArtistId artistId);
    Task<Gig> UpdateAsync(Gig gig);
    Task DeleteAsync(GigId id);
    Task<List<Gig>> GetEnrichmentCandidatesAsync();
}
