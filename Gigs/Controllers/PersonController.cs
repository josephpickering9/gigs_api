using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Gigs.DTOs;
using Gigs.Models;
using Gigs.Services;
using Gigs.Types;

namespace Gigs.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class PersonController(Database db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GetPersonResponse>>> GetAll()
    {
        var people = await db.Person
            .OrderBy(p => p.Name)
            .Select(p => new GetPersonResponse
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug
            })
            .ToListAsync();
        
        return Ok(people);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GetPersonResponse>> GetById(PersonId id)
    {
        var person = await db.Person.FindAsync(id);
        
        if (person == null)
        {
            return NotFound($"Person with ID {id} not found.");
        }

        return Ok(new GetPersonResponse
        {
            Id = person.Id,
            Name = person.Name,
            Slug = person.Slug
        });
    }

    [HttpPost]
    public async Task<ActionResult<GetPersonResponse>> Create(UpsertPersonRequest request)
    {
        // Check if person with same name already exists
        var existing = await db.Person
            .FirstOrDefaultAsync(p => p.Name.ToLower() == request.Name.ToLower());
        
        if (existing != null)
        {
            return Conflict($"A person with the name '{request.Name}' already exists.");
        }

        var person = new Person
        {
            Name = request.Name,
            Slug = Guid.NewGuid().ToString()
        };

        db.Person.Add(person);
        await db.SaveChangesAsync();

        var response = new GetPersonResponse
        {
            Id = person.Id,
            Name = person.Name,
            Slug = person.Slug
        };

        return CreatedAtAction(nameof(GetById), new { id = person.Id }, response);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GetPersonResponse>> Update(PersonId id, UpsertPersonRequest request)
    {
        var person = await db.Person.FindAsync(id);
        
        if (person == null)
        {
            return NotFound($"Person with ID {id} not found.");
        }

        // Check if another person with same name exists
        var existing = await db.Person
            .FirstOrDefaultAsync(p => p.Name.ToLower() == request.Name.ToLower() && p.Id != id);
        
        if (existing != null)
        {
            return Conflict($"Another person with the name '{request.Name}' already exists.");
        }

        person.Name = request.Name;
        await db.SaveChangesAsync();

        return Ok(new GetPersonResponse
        {
            Id = person.Id,
            Name = person.Name,
            Slug = person.Slug
        });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(PersonId id)
    {
        var person = await db.Person.FindAsync(id);
        
        if (person == null)
        {
            return NotFound($"Person with ID {id} not found.");
        }

        db.Person.Remove(person);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
