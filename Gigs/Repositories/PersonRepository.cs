using Microsoft.EntityFrameworkCore;
using Gigs.Models;
using Gigs.Services;
using Gigs.Types;
using Microsoft.EntityFrameworkCore;

namespace Gigs.Repositories;

public class PersonRepository(Database database)
{
    public async Task<PersonId> GetOrCreateAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Person name cannot be empty.");
        }

        var person = database.Person.Local.FirstOrDefault(p => p.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                     ?? await database.Person.FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower());

        if (person == null)
        {
            person = new Person
            {
                Name = name,
                Slug = Guid.NewGuid().ToString(),
            };
            database.Person.Add(person);
            await database.SaveChangesAsync();
        }

        return person.Id;
    }
}
