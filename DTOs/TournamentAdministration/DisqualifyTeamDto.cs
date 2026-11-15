using System.ComponentModel.DataAnnotations;

namespace Beckend.DTOs.TournamentAdministration;

public class DisqualifyTeamDto
{
    [Required]
    public string TeamId { get; set; } = string.Empty;

    [Required]
    [MinLength(5)]
    public string Reason { get; set; } = string.Empty;
}
