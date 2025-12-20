using Gigs.DTOs;
using Gigs.Models;
using Gigs.Types;

namespace Gigs.Services;

public interface IGigService
{
    Task<List<GetGigResponse>> GetAllAsync(GetGigsFilter filter);
    Task<GetGigResponse> GetByIdAsync(GigId id);
    Task<GetGigResponse> CreateAsync(UpsertGigRequest request);
    Task<GetGigResponse> UpdateAsync(GigId id, UpsertGigRequest request);
    Task<GetGigResponse> EnrichGigAsync(GigId id);
    Task<int> EnrichAllGigsAsync();
    Task DeleteAsync(GigId id);
}
