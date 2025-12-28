using System.ComponentModel.DataAnnotations;
using Gigs.Types;

namespace Gigs.Models;

public class GigArtist
{
    [Required]
    public GigArtistId Id { get; set; }

    [Required]
    public GigId GigId { get; set; }

    public Gig Gig { get; set; } = null!;

    [Required]
    public ArtistId ArtistId { get; set; }

    public Artist Artist { get; set; } = null!;

    public bool IsHeadliner { get; set; }

    public int Order { get; set; }

    public string? SetlistUrl { get; set; }

    public List<GigArtistSong> Songs { get; set; } =[];
}
