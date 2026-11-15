namespace Beckend.Services.TournamentAdministration;

public class DisqualificationMatchDecision
{
    public string MatchId { get; set; } = string.Empty;
    public string WinnerTeamId { get; set; } = string.Empty;

    public int HomeScore { get; set; }
    public int AwayScore { get; set; }

    public bool AdvancesWinner { get; set; }
}
