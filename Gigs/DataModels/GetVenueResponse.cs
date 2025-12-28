using Gigs.Types;

namespace Gigs.DataModels;

public class GetVenueResponse
{
    public VenueId Id { get; set; } = VenueId.New();

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string Slug { get; set; } = string.Empty;
}
