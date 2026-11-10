using Beckend.Enums;
namespace Beckend.Models.TournamentPlanning;

public class TournamentPlan
{
    public TournamentType Type { get; set; }

    public int ParticipantCount { get; set; }
    public int PlayoffSize { get; set; }

    public int GroupsCount { get; set; }
    public int TeamsPerGroup { get; set; }
    public int AdvancingPerGroup { get; set; }

    public int WinsToAdvance { get; set; }
    public int LossesToEliminate { get; set; }

    public int EstimatedMatches { get; set; }
    public int EstimatedRounds { get; set; }

    public bool IsValid { get; set; }
    public bool IsRecommended { get; set; }
    public bool IsAlternative { get; set; }

    public double Score { get; set; }
    public string FormatDescription { get; set; } = "";

    public List<string> Explanations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
