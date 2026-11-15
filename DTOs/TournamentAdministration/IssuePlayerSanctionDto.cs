using System.ComponentModel.DataAnnotations;
using Beckend.Enums;

namespace Beckend.DTOs.TournamentAdministration;

public class IssuePlayerSanctionDto
{
    [Required]
    public string TeamId { get; set; } = string.Empty;

    [Required]
    public string PlayerId { get; set; } = string.Empty;

    public PlayerSanctionType Type { get; set; }

    [Required]
    [MinLength(5)]
    public string Reason { get; set; } = string.Empty;

    public DateTime? ExpiresAt { get; set; }
}
