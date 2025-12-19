using System.ComponentModel.DataAnnotations;
using Gigs.Models;
using Gigs.Types;

namespace Gigs.DTOs;

public class UpsertGigRequest
{
    [Required]
    public VenueId VenueId { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    public decimal? TicketCost { get; set; }

    [Required]
    public TicketType TicketType { get; set; }

    public string? ImageUrl { get; set; }
    
    // For simplicity in this iteration, we might just take IDs or basic info for acts/attendees
    // but the requirement asked for "relating elements". 
    // Let's assume for now we are just managing the core Gig properties and maybe linked generic IDs if needed.
    // The user asked to "add/update/get/delete a gig and it's relating elements".
    // "Relating elements" likely implies Acts (Artists) and Attendees (People).
    // For a creation request, we usually pass IDs.
    
    public List<ArtistId> ArtistIds { get; set; } = [];
}
