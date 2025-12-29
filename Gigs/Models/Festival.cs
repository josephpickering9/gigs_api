using System.ComponentModel.DataAnnotations;
using Gigs.Types;

namespace Gigs.Models;

public class Festival
{
    [Required]
    public FestivalId Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Slug { get; set; } = Guid.NewGuid().ToString();

    public int? Year { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public decimal? Price { get; set; }

    public string? ImageUrl { get; set; }

    public string? PosterImageUrl { get; set; }

    public VenueId? VenueId { get; set; }
    public Venue? Venue { get; set; }

    public List<Gig> Gigs { get; set; } =[];

    public List<FestivalAttendee> Attendees { get; set; } = [];
}
