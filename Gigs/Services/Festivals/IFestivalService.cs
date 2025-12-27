using Gigs.DTOs;
using Gigs.Types;

namespace Gigs.Services;

public interface IFestivalService
{
    Task<List<FestivalDto>> GetAllAsync();
    Task<FestivalDto?> GetByIdAsync(FestivalId id);
    Task<FestivalDto> CreateAsync(UpsertFestivalRequest request);
    Task<FestivalDto> UpdateAsync(FestivalId id, UpsertFestivalRequest request);
    Task DeleteAsync(FestivalId id);
}
