using Beckend.Enums;
using System.ComponentModel.DataAnnotations;

namespace Beckend.DTOs;

public class AdminMatchResultDto
{
    [Required]
    public MatchResultType ResultType { get; set; }

    [Required]
    public string WinnerTeamId { get; set; } = string.Empty;

    [Required]
    [MinLength(5)]
    public string Reason { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int HomeScore { get; set; }

    [Range(0, int.MaxValue)]
    public int AwayScore { get; set; }
}
