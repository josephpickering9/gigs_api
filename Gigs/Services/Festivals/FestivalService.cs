using Gigs.DTOs;
using Gigs.Exceptions;
using Gigs.Models;
using Gigs.Repositories;
using Gigs.Types;

namespace Gigs.Services;

public class FestivalService(FestivalRepository repository, GigService gigService)
{
    public async Task<Result<List<FestivalDto>>> GetAllAsync()
    {
        var festivals = await repository.GetAllAsync();
        return festivals.Select(MapToDto).ToList().ToSuccess();
    }

    public async Task<Result<FestivalDto>> GetByIdAsync(FestivalId id)
    {
        var festival = await repository.GetByIdAsync(id);
        if (festival == null)
        {
            return Result.NotFound<FestivalDto>($"Festival with ID {id} not found.");
        }

        var dto = MapToDto(festival);

        var gigsResult = await gigService.GetAllAsync(new GetGigsFilter
        {
            FestivalId = id,
            PageSize = 100 // Reasonable limit
        });

        if (gigsResult.IsSuccess && gigsResult.Data != null)
        {
            dto.Gigs = gigsResult.Data.Items;
        }
        else
        {
            dto.Gigs =[];
        }

        return dto.ToSuccess();
    }

    public async Task<Result<FestivalDto>> CreateAsync(UpsertFestivalRequest request)
    {
        var festival = new Festival
        {
            Name = request.Name,
            Year = request.Year,
            ImageUrl = request.ImageUrl,
            Slug = Guid.NewGuid().ToString() // Simple slug for now
        };

        await repository.AddAsync(festival);

        return MapToDto(festival).ToSuccess();
    }

    public async Task<Result<FestivalDto>> UpdateAsync(FestivalId id, UpsertFestivalRequest request)
    {
        var festival = await repository.GetByIdAsync(id);
        if (festival == null)
        {
            return Result.NotFound<FestivalDto>($"Festival with ID {id} not found.");
        }

        festival.Name = request.Name;
        festival.Year = request.Year;
        festival.ImageUrl = request.ImageUrl;

        await repository.UpdateAsync(festival);

        return MapToDto(festival).ToSuccess();
    }

    public async Task<Result<bool>> DeleteAsync(FestivalId id)
    {
        var festival = await repository.GetByIdAsync(id);
        if (festival == null)
        {
            return Result.NotFound<bool>($"Festival with ID {id} not found.");
        }

        await repository.DeleteAsync(id);
        return true.ToSuccess();
    }

    private static FestivalDto MapToDto(Festival festival)
    {
        return new FestivalDto
        {
            Id = festival.Id,
            Name = festival.Name,
            Year = festival.Year,
            Slug = festival.Slug,
            ImageUrl = festival.ImageUrl,
            Gigs = festival.Gigs.Select(g => new GetGigResponse
            {
                Id = g.Id,
                VenueId = g.VenueId,
                VenueName = g.Venue.Name,
                FestivalId = g.FestivalId,
                FestivalName = g.Festival?.Name,
                Date = g.Date,
                TicketCost = g.TicketCost,
                TicketType = g.TicketType,
                ImageUrl = g.ImageUrl,
                Slug = g.Slug,
                Acts = g.Acts.Select(a => new GetGigArtistResponse
                {
                    ArtistId = a.ArtistId,
                    Name = a.Artist.Name,
                    IsHeadliner = a.IsHeadliner,
                    ImageUrl = a.Artist.ImageUrl,
                    Setlist = a.Songs.Select(s => s.Song.Title).ToList(),
                }).ToList()
            }).ToList()
        };
    }
}
