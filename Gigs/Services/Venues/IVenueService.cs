using Gigs.DTOs;
using Gigs.Types;

namespace Gigs.Services;

public interface IVenueService
{
    Task<List<GetVenueResponse>> GetAllAsync();
    Task<GetVenueResponse> EnrichVenueAsync(VenueId id);
}
