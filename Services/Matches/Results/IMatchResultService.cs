using Beckend.DTOs;
using MongoDB.Driver;
using Beckend.DTOs.Match;
namespace Beckend.Services.Matches.Results;

public interface IMatchResultService
{
    Task<MatchDto> UpdateResultAsync(string matchId,UpdateMatchResultDto dto);

    Task<MatchDto> SetAdministrativeResultAsync(string matchId, AdminMatchResultDto dto,string administratorId);
    Task<MatchDto> SetAdministrativeResultAsync(IClientSessionHandle session,string matchId,AdminMatchResultDto dto,string administratorId);
    Task<List<MatchDto>> SetAdministrativeResultsBatchAsync(
    IClientSessionHandle session,
    IReadOnlyCollection<AdministrativeMatchResultRequest> requests,
    string administratorId);
    Task FinalizeTournamentIfReadyAsync(
    IClientSessionHandle session,
    string tournamentId);
}