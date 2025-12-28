using Gigs.DataModels;
using Gigs.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class AttendeeController(Database db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GetAttendeeResponse>>> GetAll()
    {
        var attendees = await db.Person
            .OrderBy(p => p.Name)
            .Select(p => new GetAttendeeResponse
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug
            })
            .ToListAsync();

        return Ok(attendees);
    }
}
