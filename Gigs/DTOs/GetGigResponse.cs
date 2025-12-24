using Gigs.Models;
using Gigs.Types;

namespace Gigs.DTOs;

public class GetGigResponse
{
    public GigId Id { get; set; }
    public VenueId VenueId { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public FestivalId? FestivalId { get; set; }
    public string? FestivalName { get; set; }
    public DateOnly Date { get; set; }
    public decimal? TicketCost { get; set; }
    public TicketType TicketType { get; set; }
    public string? ImageUrl { get; set; }
    public string Slug { get; set; } = string.Empty;
    public List<GetGigArtistResponse> Acts { get; set; } = [];
    public List<GetGigAttendeeResponse> Attendees { get; set; } = [];
}
