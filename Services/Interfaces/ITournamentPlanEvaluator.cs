using Beckend.DTOs.TournamentPlanning;
using Beckend.Models.TournamentPlanning;

namespace Beckend.Services.Interfaces;

public interface ITournamentPlanEvaluator
{
    void Evaluate(
        TournamentPlan plan,
        TournamentPlanningRequestDto request);
}
