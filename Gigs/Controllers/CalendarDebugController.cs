using Gigs.Services.Calendar;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CalendarDebugController : ControllerBase
{
    private readonly GoogleCalendarService _calendarService;
    private readonly Services.Database _db;

    public CalendarDebugController(GoogleCalendarService calendarService, Services.Database db)
    {
        _calendarService = calendarService;
        _db = db;
    }

    /// <summary>
    /// Debug endpoint to see what locations are in calendar events vs what venues exist
    /// </summary>
    [HttpGet("debug")]
    public async Task<IActionResult> Debug([FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        var result = await _calendarService.GetCalendarEventsAsync(startDate, endDate);
        var events = result.IsSuccess && result.Data != null ? result.Data : new List<Gigs.DTOs.CalendarEventDto>();
        var venues = await _db.Venue.Select(v => new { v.Name, v.City }).ToListAsync();
        
        var eventLocations = events
            .Where(e => !string.IsNullOrWhiteSpace(e.Location))
            .Select(e => e.Location)
            .Distinct()
            .OrderBy(l => l)
            .Take(50)
            .ToList();

        return Ok(new
        {
            TotalEvents = events.Count,
            EventsWithLocation = events.Count(e => !string.IsNullOrWhiteSpace(e.Location)),
            EventsWithoutLocation = events.Count(e => string.IsNullOrWhiteSpace(e.Location)),
            SampleEventLocations = eventLocations,
            TotalVenues = venues.Count,
            SampleVenues = venues.Take(20)
        });
    }
}

