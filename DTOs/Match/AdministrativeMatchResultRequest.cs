using Beckend.DTOs;

namespace Beckend.DTOs.Match;

public class AdministrativeMatchResultRequest
{
    public string MatchId { get; set; } = string.Empty;

    public AdminMatchResultDto Result { get; set; } = new();
}