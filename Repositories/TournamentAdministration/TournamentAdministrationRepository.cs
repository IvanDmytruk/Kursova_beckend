using Beckend.DTOs;
using Beckend.DTOs.Match;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Services.TournamentAdministration;
using MongoDB.Driver;

namespace Beckend.Repositories.TournamentAdministration;

public class TournamentAdministrationRepository
    : ITournamentAdministrationRepository
{
    private readonly IMongoCollection<TournamentParticipantState>
        _participants;

    private readonly IMongoCollection<PlayerSanction> _sanctions;

    private readonly IMongoCollection<TournamentAuditEntry> _audit;

    public TournamentAdministrationRepository(
        IMongoDatabase database)
    {
        _participants = database.GetCollection<TournamentParticipantState>(
            "TournamentParticipantStates");
        var indexKeys = Builders<TournamentParticipantState>.IndexKeys
        .Ascending(x => x.TournamentId)
        .Ascending(x => x.TeamId);

        var indexOptions = new CreateIndexOptions
        {
            Unique = true,
            Name = "ux_tournament_participant_state"
        };

        _participants.Indexes.CreateOne(
            new CreateIndexModel<TournamentParticipantState>(
                indexKeys,
                indexOptions));
        _sanctions = database.GetCollection<PlayerSanction>(
            "PlayerSanctions");

        _audit = database.GetCollection<TournamentAuditEntry>(
            "TournamentAudit");
    }
    public Task<List<TournamentParticipantState>> GetParticipantStatesAsync(
    IClientSessionHandle session,
    string tournamentId)
    {
        return _participants
            .Find(session, x => x.TournamentId == tournamentId)
            .ToListAsync();
    }
    public async Task<TournamentParticipantState?> GetParticipantStateAsync(
        string tournamentId, string teamId)
    {
        return await _participants.Find(x =>
            x.TournamentId == tournamentId &&
            x.TeamId == teamId).FirstOrDefaultAsync();
    }
    public Task<List<TournamentParticipantState>> GetParticipantStatesAsync(
        string tournamentId)
    {
        return _participants.Find(x =>
            x.TournamentId == tournamentId).ToListAsync();
    }

    public async Task SaveParticipantStateAsync(
        IClientSessionHandle session,
        TournamentParticipantState state)
    {
        var filter = Builders<TournamentParticipantState>.Filter.And(
            Builders<TournamentParticipantState>.Filter.Eq(
                x => x.TournamentId, state.TournamentId),
            Builders<TournamentParticipantState>.Filter.Eq(
                x => x.TeamId, state.TeamId));

        await _participants.ReplaceOneAsync(
            session,
            filter,
            state,
            new ReplaceOptions { IsUpsert = true });
    }

    public Task AddSanctionAsync(
        IClientSessionHandle session,
        PlayerSanction sanction)
    {
        return _sanctions.InsertOneAsync(session, sanction);
    }

    public async Task<PlayerSanction?> GetSanctionAsync(string sanctionId)
    {
        return await _sanctions.Find(x =>
            x.Id == sanctionId).FirstOrDefaultAsync();
    }

    public Task<List<PlayerSanction>> GetPlayerSanctionsAsync(
        string tournamentId, string playerId)
    {
        return _sanctions.Find(x =>
            x.TournamentId == tournamentId &&
            x.PlayerId == playerId).ToListAsync();
    }

    public async Task<bool> RevokeSanctionAsync(
        IClientSessionHandle session,
        string sanctionId,
        string administratorId,
        string reason)
    {
        var filter = Builders<PlayerSanction>.Filter.And(
            Builders<PlayerSanction>.Filter.Eq(
                x => x.Id, sanctionId),
            Builders<PlayerSanction>.Filter.Eq(
                x => x.IsRevoked, false));

        var update = Builders<PlayerSanction>.Update
            .Set(x => x.IsRevoked, true)
            .Set(x => x.RevokedBy, administratorId)
            .Set(x => x.RevokedAt, DateTime.UtcNow)
            .Set(x => x.RevocationReason, reason);

        var result = await _sanctions.UpdateOneAsync(
            session, filter, update);

        return result.ModifiedCount == 1;
    }

    public Task AddAuditAsync(
        IClientSessionHandle session,
        TournamentAuditEntry entry)
    {
        return _audit.InsertOneAsync(session, entry);
    }

    public Task<List<TournamentAuditEntry>> GetAuditAsync(
        string tournamentId)
    {
        return _audit.Find(x =>
            x.TournamentId == tournamentId)
            .SortByDescending(x => x.CreatedAt)
            .ToListAsync();
    }
    public Task<List<PlayerSanction>> GetTeamSanctionsAsync(string tournamentId,string teamId)
    {
        return _sanctions.Find(x =>
            x.TournamentId == tournamentId &&
            x.TeamId == teamId &&
            !x.IsRevoked).ToListAsync();
    }

    public async Task<bool> DisqualifyParticipantAsync(
        IClientSessionHandle session,
        TournamentParticipantState state)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(state);

        if (!session.IsInTransaction)
            throw new InvalidOperationException(
                "An active transaction is required.");

        var filter = Builders<TournamentParticipantState>.Filter.And(
            Builders<TournamentParticipantState>.Filter.Eq(
                x => x.TournamentId, state.TournamentId),
            Builders<TournamentParticipantState>.Filter.Eq(
                x => x.TeamId, state.TeamId),
            Builders<TournamentParticipantState>.Filter.Eq(
                x => x.Status, TournamentParticipantStatus.Active)
        );

        var update = Builders<TournamentParticipantState>.Update
            .Set(x => x.Status, TournamentParticipantStatus.Disqualified)
            .Set(x => x.Reason, state.Reason)
            .Set(x => x.UpdatedBy, state.UpdatedBy)
            .Set(x => x.UpdatedAt, state.UpdatedAt);

        var result = await _participants.UpdateOneAsync(
            session, filter, update);

        return result.ModifiedCount == 1;
    }

    private static List<AdministrativeMatchResultRequest>BuildDisqualificationRequests(
            IReadOnlyCollection<DisqualificationMatchDecision> decisions,
            string reason)
    {
        return decisions.Select(decision =>
            new AdministrativeMatchResultRequest
            {
                MatchId = decision.MatchId,

                Result = new AdminMatchResultDto
                {
                    ResultType = MatchResultType.Disqualification,
                    WinnerTeamId = decision.WinnerTeamId,
                    HomeScore = decision.HomeScore,
                    AwayScore = decision.AwayScore,
                    Reason = reason
                }
            }).ToList();
    }
    public Task InsertParticipantStateAsync(
    IClientSessionHandle session,
    TournamentParticipantState state)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(state);

        if (!session.IsInTransaction)
            throw new InvalidOperationException(
                "An active MongoDB transaction is required.");

        return _participants.InsertOneAsync(session, state);
    }
}
