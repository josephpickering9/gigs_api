using Gigs.Types;

namespace Gigs.DataModels;

public class GigFilterCriteria
{
    public VenueId? VenueId { get; set; }

    public FestivalId? FestivalId { get; set; }

    public string? City { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public ArtistId? ArtistId { get; set; }

    public PersonId? AttendeeId { get; set; }
}
