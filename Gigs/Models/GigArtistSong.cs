using System.ComponentModel.DataAnnotations;
using Gigs.Types;

namespace Gigs.Models;

public class GigArtistSong
{
    [Required] public GigArtistId GigArtistId { get; set; }
    public GigArtist GigArtist { get; set; } = null!;

    [Required] public SongId SongId { get; set; }
    public Song Song { get; set; } = null!;

    public int Order { get; set; }
}
