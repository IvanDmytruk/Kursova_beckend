using Beckend.DTOs.TournamentAdministration;
using Beckend.Models;

namespace Beckend.Services.TournamentAdministration;

public interface ITournamentAdministrationService
{
    Task<PlayerSanction> IssueSanctionAsync(
        string tournamentId,
        IssuePlayerSanctionDto dto,
        string administratorId);

    Task RevokeSanctionAsync(
        string tournamentId,
        string sanctionId,
        string reason,
        string administratorId);

    Task<List<PlayerSanction>> GetPlayerSanctionsAsync(
        string tournamentId,
        string playerId,
        string requesterId);

    Task<List<TournamentAuditEntry>> GetAuditAsync(
        string tournamentId,
        string requesterId);
    Task<TournamentParticipantState> DisqualifyTeamAsync(
    string tournamentId,
    DisqualifyTeamDto dto,
    string administratorId);
}
