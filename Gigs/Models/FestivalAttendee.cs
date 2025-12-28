using System.ComponentModel.DataAnnotations;
using Gigs.Types;

namespace Gigs.Models;

public class FestivalAttendee
{
    [Required]
    public FestivalId FestivalId { get; set; }

    public Festival Festival { get; set; } = null!;

    [Required]
    public PersonId PersonId { get; set; }

    public Person Person { get; set; } = null!;
}
