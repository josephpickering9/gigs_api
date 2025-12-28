using System.ComponentModel.DataAnnotations;
using Gigs.Types;

namespace Gigs.Models;

public class Artist
{
    [Required]
    public ArtistId Id { get; set; }

    [Required]
    public string Name { get; set; } = null!;

    public string? ImageUrl { get; set; }

    [Required]
    public string Slug { get; set; } = Guid.NewGuid().ToString();

    public List<GigArtist> Gigs { get; set; } =[];
}
