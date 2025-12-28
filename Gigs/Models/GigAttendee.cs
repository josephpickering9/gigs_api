using System.ComponentModel.DataAnnotations;
using Gigs.Types;

namespace Gigs.Models;

public class GigAttendee
{
    [Required]
    public GigId GigId { get; set; }

    public Gig Gig { get; set; } = null!;

    [Required]
    public PersonId PersonId { get; set; }

    public Person Person { get; set; } = null!;
}
