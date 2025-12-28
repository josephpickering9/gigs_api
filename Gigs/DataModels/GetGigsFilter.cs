using Gigs.Types;

namespace Gigs.DTOs;

public class GetGigsFilter
{
    public VenueId? VenueId { get; set; }

    public FestivalId? FestivalId { get; set; }

    public string? City { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    public ArtistId? ArtistId { get; set; }

    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public PersonId? AttendeeId { get; set; }
}
