using Beckend.DTOs;

namespace Beckend.Services.Matches.Administration;

public interface IMatchAdministrationService
{
    Task<MatchDto> SetTechnicalResultAsync(
        string matchId,
        AdminMatchResultDto dto,
        string administratorId);
}
