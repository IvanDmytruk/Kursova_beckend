using System.ComponentModel.DataAnnotations;

namespace Beckend.DTOs.TournamentAdministration;

public class RevokeSanctionDto
{
    [Required]
    [MinLength(5)]
    public string Reason { get; set; } = string.Empty;
}
