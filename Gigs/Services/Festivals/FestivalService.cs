using Gigs.DTOs;
using Gigs.Exceptions;
using Gigs.Models;
using Gigs.Types;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Services;

public class FestivalService(Database db, IGigService gigService) : IFestivalService
{
    public async Task<List<FestivalDto>> GetAllAsync()
    {
        var festivals = await db.Festival
            .OrderBy(f => f.Name)
            .ToListAsync();
            
        return festivals.Select(MapToDto).ToList();
    }

    public async Task<FestivalDto?> GetByIdAsync(FestivalId id)
    {
        var festival = await db.Festival.FindAsync(id);
        if (festival == null) return null;
        
        var dto = MapToDto(festival);
        
        var gigs = await gigService.GetAllAsync(new GetGigsFilter
        {
            FestivalId = id,
            PageSize = 100 // Reasonable limit
        });
        
        dto.Gigs = gigs.Items;
        
        return dto;
    }

    public async Task<FestivalDto> CreateAsync(UpsertFestivalRequest request)
    {
        var festival = new Festival
        {
            Name = request.Name,
            Year = request.Year,
            ImageUrl = request.ImageUrl,
            Slug = Guid.NewGuid().ToString() // Simple slug for now
        };
        
        db.Festival.Add(festival);
        await db.SaveChangesAsync();
        
        return MapToDto(festival);
    }

    public async Task<FestivalDto> UpdateAsync(FestivalId id, UpsertFestivalRequest request)
    {
        var festival = await db.Festival.FindAsync(id);
        if (festival == null)
        {
            throw new NotFoundException($"Festival with ID {id} not found.");
        }
        
        festival.Name = request.Name;
        festival.Year = request.Year;
        festival.ImageUrl = request.ImageUrl;
        
        await db.SaveChangesAsync();
        
        return MapToDto(festival);
    }

    public async Task DeleteAsync(FestivalId id)
    {
        var festival = await db.Festival.FindAsync(id);
        if (festival == null) return;
        
        db.Festival.Remove(festival);
        await db.SaveChangesAsync();
    }

    private static FestivalDto MapToDto(Festival festival)
    {
        return new FestivalDto
        {
            Id = festival.Id,
            Name = festival.Name,
            Year = festival.Year,
            Slug = festival.Slug,
            ImageUrl = festival.ImageUrl
        };
    }
}
