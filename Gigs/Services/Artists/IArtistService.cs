using Gigs.DTOs;
using Gigs.Types;

namespace Gigs.Services;

public interface IArtistService
{
    Task<List<GetArtistResponse>> GetAllAsync();
    Task<GetArtistResponse> EnrichArtistAsync(ArtistId id);
    Task<int> EnrichAllArtistsAsync();
}
