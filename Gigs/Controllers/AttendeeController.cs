using Gigs.DataModels;
using Gigs.Models;
using Gigs.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class AttendeeController(Database db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GetAttendeeResponse>>> GetAll([FromQuery] GigFilterCriteria? filter)
    {
        var people = await db.Person
            .Include(p => p.Gigs)
                .ThenInclude(ga => ga.Gig)
                    .ThenInclude(g => g.Venue)
            .Include(p => p.Gigs)
                .ThenInclude(ga => ga.Gig)
                    .ThenInclude(g => g.Acts)
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync();

        // Apply filters in memory after materialization
        var filteredPeople = people;
        if (filter != null && HasAnyFilter(filter))
        {
            filteredPeople = people
                .Where(p => p.Gigs.Any(ga => MatchesFilter(ga.Gig, filter)))
                .ToList();
        }

        var attendees = filteredPeople.Select(p => new GetAttendeeResponse
        {
            Id = p.Id,
            Name = p.Name,
            Slug = p.Slug,
            GigCount = filter != null && HasAnyFilter(filter)
                ? p.Gigs.Count(ga => MatchesFilter(ga.Gig, filter))
                : p.Gigs.Count
        }).ToList();

        return Ok(attendees);
    }

    private static bool HasAnyFilter(GigFilterCriteria filter)
    {
        return filter.VenueId.HasValue
            || filter.FestivalId.HasValue
            || !string.IsNullOrWhiteSpace(filter.City)
            || filter.FromDate.HasValue
            || filter.ToDate.HasValue
            || filter.ArtistId.HasValue
            || filter.AttendeeId.HasValue;
    }

    private static bool MatchesFilter(Gig gig, GigFilterCriteria filter)
    {
        if (filter.VenueId.HasValue && gig.VenueId != filter.VenueId.Value)
            return false;

        if (filter.FestivalId.HasValue && gig.FestivalId != filter.FestivalId.Value)
            return false;

        if (!string.IsNullOrWhiteSpace(filter.City) && gig.Venue != null &&
            !gig.Venue.City.Equals(filter.City, StringComparison.OrdinalIgnoreCase))
            return false;

        if (filter.FromDate.HasValue && gig.Date < filter.FromDate.Value)
            return false;

        if (filter.ToDate.HasValue && gig.Date > filter.ToDate.Value)
            return false;

        if (filter.ArtistId.HasValue && !gig.Acts.Any(a => a.ArtistId == filter.ArtistId.Value))
            return false;

        // Skip AttendeeId filter in attendees endpoint to avoid circular reference
        // if (filter.AttendeeId.HasValue && !gig.Attendees.Any(a => a.PersonId == filter.AttendeeId.Value))
        //     return false;

        return true;
    }
}
