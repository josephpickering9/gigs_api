using System.ComponentModel.DataAnnotations;

namespace Gigs.DTOs;

public class UpsertPersonRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
