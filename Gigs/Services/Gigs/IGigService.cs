using Gigs.DTOs;
using Gigs.Models;
using Gigs.Types;

namespace Gigs.Services;

public interface IGigService
{
    Task<List<GigDto>> GetAllAsync();
    Task<GigDto> GetByIdAsync(GigId id);
    Task<GigDto> CreateAsync(UpsertGigRequest request);
    Task<GigDto> UpdateAsync(GigId id, UpsertGigRequest request);
    Task DeleteAsync(GigId id);
}
