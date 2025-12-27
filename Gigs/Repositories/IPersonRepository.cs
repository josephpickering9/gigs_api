using Gigs.Models;
using Gigs.Types;

namespace Gigs.Repositories;

public interface IPersonRepository
{
    Task<PersonId> GetOrCreateAsync(string name);
}
