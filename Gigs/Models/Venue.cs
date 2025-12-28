using System.ComponentModel.DataAnnotations;
using Gigs.Types;

namespace Gigs.Models;

public class Venue
{
    [Required]
    public VenueId Id { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    [Required]
    public string City { get; set; } = null!;

    public string? ImageUrl { get; set; }

    [Required]
    public string Slug { get; set; } = Guid.NewGuid().ToString();

    public List<Gig> Gigs { get; set; } =[];
}
