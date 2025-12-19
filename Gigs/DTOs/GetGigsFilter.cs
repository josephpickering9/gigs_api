using Gigs.Types;

namespace Gigs.DTOs;

public class GetGigsFilter
{
    public VenueId? VenueId { get; set; }
    public string? City { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public ArtistId? ArtistId { get; set; }
}
