using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Repositories.TournamentAdministration;

public interface ITournamentAdministrationRepository
{
    Task<List<TournamentParticipantState>> GetParticipantStatesAsync( IClientSessionHandle session,string tournamentId);
    Task<TournamentParticipantState?> GetParticipantStateAsync(
        string tournamentId, string teamId);

    Task<List<TournamentParticipantState>> GetParticipantStatesAsync(
        string tournamentId);

    Task SaveParticipantStateAsync(
        IClientSessionHandle session,
        TournamentParticipantState state);

    Task AddSanctionAsync(
        IClientSessionHandle session,
        PlayerSanction sanction);

    Task<PlayerSanction?> GetSanctionAsync(string sanctionId);

    Task<List<PlayerSanction>> GetPlayerSanctionsAsync(
        string tournamentId, string playerId);

    Task<bool> RevokeSanctionAsync(
        IClientSessionHandle session,
        string sanctionId,
        string administratorId,
        string reason);

    Task AddAuditAsync(
        IClientSessionHandle session,
        TournamentAuditEntry entry);

    Task<List<TournamentAuditEntry>> GetAuditAsync(
        string tournamentId);
    Task<List<PlayerSanction>> GetTeamSanctionsAsync(
    string tournamentId,
    string teamId);
    Task<bool> DisqualifyParticipantAsync(
    IClientSessionHandle session,
    TournamentParticipantState state);
    Task InsertParticipantStateAsync(
    IClientSessionHandle session,
    TournamentParticipantState state);
}
