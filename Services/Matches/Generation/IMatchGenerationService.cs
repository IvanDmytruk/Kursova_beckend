using Beckend.DTOs;

namespace Beckend.Services.Matches.Generation;

public interface IMatchGenerationService
{
    Task<List<MatchDto>> GenerateRoundRobinAsync(string tournamentId);

    Task<List<MatchDto>> GenerateSwissRoundAsync(string tournamentId);
    Task<List<MatchDto>> GenerateGroupStageAsync(string tournamentId);
    Task<List<MatchDto>> GenerateDoubleEliminationRoundAsync(string tournamentId);
}