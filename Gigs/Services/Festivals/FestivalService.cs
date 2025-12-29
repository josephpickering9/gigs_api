using Gigs.DataModels;
using Gigs.Exceptions;
using Gigs.Models;
using Gigs.Repositories;
using Gigs.Types;

namespace Gigs.Services;

public class FestivalService(FestivalRepository repository, GigService gigService, PersonRepository personRepository, GigRepository gigRepository, VenueRepository venueRepository)
{
    public async Task<Result<List<GetFestivalResponse>>> GetAllAsync()
    {
        var festivals = await repository.GetAllAsync();
        return festivals.Select(MapToDto).ToList().ToSuccess();
    }

    public async Task<Result<GetFestivalResponse>> GetByIdAsync(FestivalId id)
    {
        var festival = await repository.GetByIdAsync(id);
        if (festival == null)
        {
            return Result.NotFound<GetFestivalResponse>($"Festival with ID {id} not found.");
        }

        var dto = MapToDto(festival);

        var gigsResult = await gigService.GetAllAsync(new GetGigsFilter
        {
            FestivalId = id,
            PageSize = 100 // Reasonable limit
        });

        if (gigsResult.IsSuccess && gigsResult.Data != null)
        {
            dto.Gigs = gigsResult.Data.Items.OrderBy(g => g.Date).ThenBy(g => g.Order).ToList();
        }
        else
        {
            dto.Gigs = [];
        }

        return dto.ToSuccess();
    }

    public async Task<Result<GetFestivalResponse>> CreateAsync(UpsertFestivalRequest request)
    {
        var festival = new Festival
        {
            Slug = Guid.NewGuid().ToString() // Simple slug for now
        };

        var venueId = await ResolveVenueId(request);

        UpdateFestivalDetails(festival, request, venueId);
        await ReconcileAttendees(festival, request.Attendees);
        await ReconcileGigOrders(festival, request.Gigs);

        await repository.AddAsync(festival);

        // Fetch again to ensure all relationships are populated if needed (though we just added it)
        // With EF, the navigation properties might not be fully populated unless we attach them or re-fetch.
        // For simple create, the entities added to lists should be available.
        // But to be safe and consistent with Update/Get, we might want to return the mapped DTO.
        // Because we manually added attendees, we have the IDs. 
        // Let's rely on the repository to fetch the full graph for the return to be 100% accurate including names.
        var createdFestival = await repository.GetByIdAsync(festival.Id);

        return MapToDto(createdFestival!).ToSuccess();
    }

    public async Task<Result<GetFestivalResponse>> UpdateAsync(FestivalId id, UpsertFestivalRequest request)
    {
        var festival = await repository.GetByIdAsync(id);
        if (festival == null)
        {
            return Result.NotFound<GetFestivalResponse>($"Festival with ID {id} not found.");
        }

        var venueId = await ResolveVenueId(request);

        UpdateFestivalDetails(festival, request, venueId);

        await ReconcileAttendees(festival, request.Attendees);
        await ReconcileGigOrders(festival, request.Gigs);

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


    private static void UpdateFestivalDetails(Festival festival, UpsertFestivalRequest request, VenueId? venueId)
    {
        festival.Name = request.Name;
        festival.Year = request.Year;
        festival.ImageUrl = request.ImageUrl;
        festival.PosterImageUrl = request.PosterImageUrl;
        festival.VenueId = venueId;
        festival.StartDate = request.StartDate;
        festival.EndDate = request.EndDate;
        festival.Price = request.Price;
    }

    private async Task ReconcileAttendees(Festival festival, List<string> requestedAttendeeNames)
    {
        var requestedPersonIds = new List<PersonId>();
        foreach (var personName in requestedAttendeeNames)
        {
            var name = ResolveNameFromId(personName);
            if (name != null)
            {
                var personId = await personRepository.GetOrCreateAsync(name);
                requestedPersonIds.Add(personId);
                continue;
            }

            if (Guid.TryParse(personName, out var guid))
            {
                requestedPersonIds.Add(IdFactory.Create<PersonId>(guid));
                continue;
            }

            var personIdByName = await personRepository.GetOrCreateAsync(personName);
            requestedPersonIds.Add(personIdByName);
        }
        var requestedAttendeeIds = requestedPersonIds.ToHashSet();
        
        var attendeesToRemove = festival.Attendees.Where(a => !requestedAttendeeIds.Contains(a.PersonId)).ToList();
        foreach (var attendee in attendeesToRemove)
        {
            festival.Attendees.Remove(attendee);
        }
        
        foreach (var personId in requestedPersonIds)
        {
            var existingAttendee = festival.Attendees.FirstOrDefault(a => a.PersonId == personId);
            if (existingAttendee == null)
            {
                festival.Attendees.Add(new FestivalAttendee
                {
                    FestivalId = festival.Id,
                    PersonId = personId
                });
            }
        }
    }

    private async Task ReconcileGigOrders(Festival festival, List<FestivalGigOrderRequest> gigOrders)
    {
        if (!gigOrders.Any()) return;

        var gigIds = new List<GigId>();
        foreach (var order in gigOrders)
        {
            if (Guid.TryParse(order.GigId, out var guid))
            {
                gigIds.Add(IdFactory.Create<GigId>(guid));
            }
        }

        foreach (var gigId in gigIds)
        {
            var gig = await gigRepository.GetByIdAsync(gigId);
            if (gig != null && gig.FestivalId == festival.Id)
            {
                var request = gigOrders.FirstOrDefault(g => g.GigId == gigId.Value.ToString());
                if (request != null)
                {
                    gig.Order = request.Order;
                    await gigRepository.UpdateAsync(gig);
                }
            }
        }
    }

    private async Task<VenueId?> ResolveVenueId(UpsertFestivalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.VenueId) && string.IsNullOrWhiteSpace(request.VenueName)) return null;

        if (Guid.TryParse(request.VenueId, out var vId))
        {
            return IdFactory.Create<VenueId>(vId);
        }

        var venueName = request.VenueName ?? ResolveNameFromId(request.VenueId);
        if (string.IsNullOrWhiteSpace(venueName))
        {
             return null;
        }

        // Default city to Unknown if not provided (Festivals usually assume valid venue existence, 
        // but let's support creation if needed, though we don't have city in request? 
        // UpsertGigRequest has City. UpsertFestival doesn't yet. 
        // Use "Unknown" as default or let GetOrCreateAsync handle it.
        return await venueRepository.GetOrCreateAsync(venueName, "Unknown");
    }

    private static string? ResolveNameFromId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        if (id.StartsWith("new:", StringComparison.OrdinalIgnoreCase))
        {
            return id.Substring(4);
        }
        return null;
    }
    private static GetFestivalResponse MapToDto(Festival festival)
    {
        var dailyPrice = festival.Price.HasValue && festival.EndDate.HasValue && festival.StartDate.HasValue
            ? festival.Price.Value / (decimal)(festival.EndDate.Value.DayNumber - festival.StartDate.Value.DayNumber + 1)
            : (decimal?)null;

        return new GetFestivalResponse
        {
            Id = festival.Id,
            Name = festival.Name,
            Year = festival.Year,
            Slug = festival.Slug,
            ImageUrl = festival.ImageUrl,
            PosterImageUrl = festival.PosterImageUrl,
            VenueId = festival.VenueId,
            VenueName = festival.Venue?.Name,
            StartDate = festival.StartDate,
            EndDate = festival.EndDate,
            Price = festival.Price,
            DailyPrice = dailyPrice,
            Attendees = festival.Attendees.Select(a => new GetPersonResponse
            {
                Id = a.PersonId,
                Name = a.Person.Name,
                Slug = a.Person.Slug
            }).ToList(),
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
                Acts = g.Acts.OrderBy(a => a.Order).Select(a => new GetGigArtistResponse
                {
                    ArtistId = a.ArtistId,
                    Name = a.Artist.Name,
                    IsHeadliner = a.IsHeadliner,
                    ImageUrl = a.Artist.ImageUrl,
                    Setlist = a.Songs.OrderBy(s => s.Order).Select(s => new GetGigSongResponse 
                    {
                        Title = s.Song.Title,
                        Order = s.Order,
                        IsEncore = s.IsEncore
                    }).ToList(),
                }).ToList()
            }).OrderBy(g => g.Date).ToList()
        };
    }
}
