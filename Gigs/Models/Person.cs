using System.ComponentModel.DataAnnotations;
using Gigs.Types;

namespace Gigs.Models;

public class Person
{
    [Required]
    public PersonId Id { get; set; } = PersonId.New();

    [Required]
    public string Name { get; set; } = null!;

    [Required]
    public string Slug { get; set; } = Guid.NewGuid().ToString();

    public List<GigAttendee> Gigs { get; set; } =[];
}
