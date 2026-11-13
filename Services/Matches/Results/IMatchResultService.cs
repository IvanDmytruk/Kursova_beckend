using Beckend.DTOs;

namespace Beckend.Services.Matches.Results;

public interface IMatchResultService
{
    Task<MatchDto> UpdateResultAsync(
        string matchId,
        UpdateMatchResultDto dto);
}