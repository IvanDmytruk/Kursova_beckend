using Beckend.DTOs.TournamentPlanning;

namespace Beckend.Services.Interfaces;

public interface ITournamentPlanningService
{
    TournamentPlanningResponseDto Suggest(
        TournamentPlanningRequestDto request);
}
