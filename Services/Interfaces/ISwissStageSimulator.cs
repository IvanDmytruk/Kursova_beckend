using Beckend.Models.TournamentPlanning;

namespace Beckend.Services.Interfaces;

public interface ISwissStageSimulator
{
    SwissSimulationResult Simulate(
        int participantCount,
        int winsToAdvance,
        int lossesToEliminate);
}
