
namespace Beckend.Models.TournamentPlanning;

public class SwissRecordGroup
{
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int TeamCount { get; set; }
}

public class SwissRoundSnapshot
{
    public int RoundNumber { get; set; }
    public int MatchesPlayed { get; set; }
    public int AdvancedCount { get; set; }
    public int EliminatedCount { get; set; }
    public List<SwissRecordGroup> ActiveGroups { get; set; } = new();
}

public class SwissSimulationResult
{
    public bool IsValid { get; set; }
    public int TotalMatches { get; set; }
    public int TotalRounds { get; set; }
    public int AdvancedCount { get; set; }
    public int EliminatedCount { get; set; }

    public List<SwissRoundSnapshot> Rounds { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
