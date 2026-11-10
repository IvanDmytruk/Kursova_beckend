
namespace Beckend.DTOs.Olympic;

public class OlympicBracketResponseDto
{
    public string TournamentId { get; set; } = "";
    public string StageId { get; set; } = "";

    public int ParticipantCount { get; set; }
    public int BracketSize { get; set; }
    public int RoundNumber { get; set; }

    public int ByesCount { get; set; }
    public int MatchesCount { get; set; }

    public bool IsCompleted { get; set; }
    public string? ChampionTeamId { get; set; }

    public List<OlympicMatchDto> Matches { get; set; } = new();

    public List<string> AutomaticAdvancements { get; set; } = new();
}

public class OlympicMatchDto
{
    public string Id { get; set; } = "";
    public string HomeTeamId { get; set; } = "";
    public string AwayTeamId { get; set; } = "";

    public int RoundNumber { get; set; }
    public int BracketPosition { get; set; }
}
