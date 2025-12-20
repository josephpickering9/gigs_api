namespace Gigs.DTOs;

public class TopVenueResponse
{
    public string VenueId { get; set; } = string.Empty;
    public string VenueName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public int GigCount { get; set; }
}
