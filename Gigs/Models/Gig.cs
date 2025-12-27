using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Gigs.Types;

namespace Gigs.Models;

public class Gig
{
    [Required] public GigId Id { get; set; } = GigId.New();

    [Required] public VenueId VenueId { get; set; }

    public Venue Venue { get; set; } = null!;
    
    public FestivalId? FestivalId { get; set; }
    public Festival? Festival { get; set; }

    [Required] public DateOnly Date { get; set; }

    public decimal? TicketCost { get; set; }

    public TicketType TicketType { get; set; }

    public string? ImageUrl { get; set; }

    [Required] public string Slug { get; set; } = Guid.NewGuid().ToString();

    public List<GigArtist> Acts { get; set; } = [];

    public List<GigAttendee> Attendees { get; set; } = [];
}

public enum TicketType
{
    [Description("Standing")] Standing, // 0
    [Description("Seated")] Seated, // 1
    [Description("VIP")] VIP, // 2
    [Description("Guest List")] GuestList, // 3
    [Description("Other")] Other // 4
}
