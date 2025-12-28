using Gigs.Types;

namespace Gigs.DTOs;

public class GetPersonResponse
{
    public PersonId Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
}
