
using Beckend.Models.TournamentPlanning;

namespace Beckend.DTOs.TournamentPlanning;

public class TournamentPlanningResponseDto
{
    public int ParticipantCount { get; set; }
    public int PlayoffSize { get; set; }

    public List<TournamentPlan> Plans { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}
