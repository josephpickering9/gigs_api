using System.ComponentModel.DataAnnotations;
using Gigs.Types;

namespace Gigs.Models;

public class Song
{
    [Required]
    public SongId Id { get; set; }

    [Required]
    public ArtistId ArtistId { get; set; }

    public Artist Artist { get; set; } = null!;

    [Required]
    public string Title { get; set; } = null!;

    [Required]
    public string Slug { get; set; } = Guid.NewGuid().ToString();
}
