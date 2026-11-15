using System.Text.Json;
using Beckend.DTOs.TournamentAdministration;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.TournamentAdministration;
using Beckend.Repositories.Tournaments;
using Beckend.Repositories.Teams;
using Beckend.Services.TournamentAccess;
using MongoDB.Driver;
using Beckend.DTOs;
using Beckend.DTOs.Match;
using Beckend.Repositories.Matches;
using Beckend.Services.Matches.Results;
using MongoDB.Bson;

namespace Beckend.Services.TournamentAdministration;

public class TournamentAdministrationService
    : ITournamentAdministrationService
{
    private readonly ITournamentAdministrationRepository _repository;
    private readonly ITournamentRepository _tournaments;
    private readonly ITeamRepository _teams;
    private readonly ITournamentAccessService _access;
    private readonly IMongoClient _mongoClient;
    private readonly IMongoCollection<TeamMember> _teamMembers;
    private readonly IMatchRepository _matches;
    private readonly IMatchResultService _matchResults;
    private readonly DisqualificationPlanner _disqualificationPlanner;

    public TournamentAdministrationService(
        ITournamentAdministrationRepository repository,
        ITournamentRepository tournaments,
        ITeamRepository teams,
        ITournamentAccessService access,
        IMongoClient mongoClient,
        IMongoDatabase database,
        IMatchRepository matches,
        IMatchResultService matchResults,
        DisqualificationPlanner disqualificationPlanner)
    {
        _repository = repository;
        _tournaments = tournaments;
        _teams = teams;
        _access = access;
        _mongoClient = mongoClient;
        _teamMembers = database.GetCollection<TeamMember>(
            "TeamMembers");
        _matches = matches;
        _matchResults = matchResults;
        _disqualificationPlanner = disqualificationPlanner;
    }

    private async Task RequirePermissionAsync(
        string tournamentId,
        string administratorId,
        TournamentPermission permission)
    {
        if (string.IsNullOrWhiteSpace(administratorId))
            throw new UnauthorizedAccessException(
                "Authentication is required.");

        if (!await _access.HasPermissionAsync(
                tournamentId, administratorId, permission))
        {
            throw new UnauthorizedAccessException(
                "Insufficient tournament permissions.");
        }
    }

    private async Task<Tournament> RequireActiveTournamentAsync(
        string tournamentId)
    {
        var tournament = await _tournaments.GetByIdAsync(
            tournamentId)
            ?? throw new KeyNotFoundException(
                "Tournament not found.");

        if (tournament.Status != TournamentStatus.InProgress)
            throw new InvalidOperationException(
                "Tournament must be InProgress.");

        return tournament;
    }

    public async Task<PlayerSanction> IssueSanctionAsync(
        string tournamentId,
        IssuePlayerSanctionDto dto,
        string administratorId)
    {
        if (dto == null)
            throw new ArgumentException("Request body is required.");

        await RequirePermissionAsync(
            tournamentId,
            administratorId,
            TournamentPermission.ManagePlayerSanctions);

        var tournament = await RequireActiveTournamentAsync(
            tournamentId);

        if (string.IsNullOrWhiteSpace(dto.TeamId) ||
            string.IsNullOrWhiteSpace(dto.PlayerId))
        {
            throw new ArgumentException(
                "TeamId and PlayerId are required.");
        }

        if (!Enum.IsDefined(dto.Type))
            throw new ArgumentException(
                "Invalid sanction type.");

        if (string.IsNullOrWhiteSpace(dto.Reason) ||
            dto.Reason.Trim().Length < 5)
        {
            throw new ArgumentException(
                "Reason must contain at least 5 characters.");
        }

        if (dto.Type == PlayerSanctionType.MatchSuspension)
        {
            if (!dto.ExpiresAt.HasValue ||
                dto.ExpiresAt.Value.ToUniversalTime() <= DateTime.UtcNow)
            {
                throw new ArgumentException(
                    "Match suspension requires a future expiration date.");
            }
        }
        else if (dto.ExpiresAt.HasValue)
        {
            throw new ArgumentException(
                "Expiration date is only allowed for MatchSuspension.");
        }

        if (!tournament.TournamentParticipants.Any(
            x => x.Id == dto.TeamId))
        {
            throw new ArgumentException(
                "Team is not a tournament participant.");
        }

        var team = await _teams.GetByIdAsync(dto.TeamId)
            ?? throw new KeyNotFoundException("Team not found.");

        if (team.SportName != tournament.SportName)
            throw new ArgumentException(
                "Team sport does not match tournament sport.");

        var membership = await _teamMembers.Find(x =>
            x.TeamId == dto.TeamId &&
            x.UserId == dto.PlayerId &&
            x.IsActive &&
            x.LeftAt == null).FirstOrDefaultAsync();

        if (membership == null)
            throw new ArgumentException(
                "Player is not an active team member.");

        var sanction = new PlayerSanction
        {
            TournamentId = tournamentId,
            TeamId = dto.TeamId,
            PlayerId = dto.PlayerId,
            Type = dto.Type,
            Reason = dto.Reason.Trim(),
            IssuedBy = administratorId,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = dto.ExpiresAt?.ToUniversalTime()
        };

        var audit = new TournamentAuditEntry
        {
            TournamentId = tournamentId,
            ActorId = administratorId,
            Action = "PlayerSanctionIssued",
            EntityType = nameof(PlayerSanction),
            EntityId = sanction.Id,
            Reason = sanction.Reason,
            BeforeJson = null,
            AfterJson = JsonSerializer.Serialize(sanction)
        };

        using var session = await _mongoClient.StartSessionAsync();

        session.StartTransaction();

        try
        {
            await _repository.AddSanctionAsync(session, sanction);
            await _repository.AddAuditAsync(session, audit);

            await session.CommitTransactionAsync();
            return sanction;
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync();

            throw;
        }
    }

    public async Task RevokeSanctionAsync(
        string tournamentId,
        string sanctionId,
        string reason,
        string administratorId)
    {
        await RequirePermissionAsync(
            tournamentId,
            administratorId,
            TournamentPermission.ManagePlayerSanctions);

        await RequireActiveTournamentAsync(tournamentId);

        if (string.IsNullOrWhiteSpace(reason) ||
            reason.Trim().Length < 5)
        {
            throw new ArgumentException(
                "Revocation reason must contain at least 5 characters.");
        }

        var sanction = await _repository.GetSanctionAsync(sanctionId)
            ?? throw new KeyNotFoundException("Sanction not found.");

        if (sanction.TournamentId != tournamentId)
            throw new KeyNotFoundException("Sanction not found.");

        if (sanction.IsRevoked)
            throw new InvalidOperationException(
                "Sanction is already revoked.");

        var beforeJson = JsonSerializer.Serialize(sanction);

        using var session = await _mongoClient.StartSessionAsync();
        session.StartTransaction();

        try
        {
            var updated = await _repository.RevokeSanctionAsync(
                session,
                sanctionId,
                administratorId,
                reason.Trim());

            if (!updated)
                throw new InvalidOperationException(
                    "Sanction was already modified.");

            sanction.IsRevoked = true;
            sanction.RevokedBy = administratorId;
            sanction.RevokedAt = DateTime.UtcNow;
            sanction.RevocationReason = reason.Trim();

            await _repository.AddAuditAsync(
                session,
                new TournamentAuditEntry
                {
                    TournamentId = tournamentId,
                    ActorId = administratorId,
                    Action = "PlayerSanctionRevoked",
                    EntityType = nameof(PlayerSanction),
                    EntityId = sanctionId,
                    Reason = reason.Trim(),
                    BeforeJson = beforeJson,
                    AfterJson = JsonSerializer.Serialize(sanction)
                });

            await session.CommitTransactionAsync();
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync();

            throw;
        }
    }

    public async Task<List<PlayerSanction>> GetPlayerSanctionsAsync(
        string tournamentId,
        string playerId,
        string requesterId)
    {
        await RequirePermissionAsync(
            tournamentId,
            requesterId,
            TournamentPermission.ManagePlayerSanctions);

        if (await _tournaments.GetByIdAsync(tournamentId) == null)
            throw new KeyNotFoundException("Tournament not found.");

        return await _repository.GetPlayerSanctionsAsync(
            tournamentId, playerId);
    }

    public async Task<List<TournamentAuditEntry>> GetAuditAsync(
        string tournamentId,
        string requesterId)
    {
        await RequirePermissionAsync(
            tournamentId,
            requesterId,
            TournamentPermission.ViewAuditLog);

        if (await _tournaments.GetByIdAsync(tournamentId) == null)
            throw new KeyNotFoundException("Tournament not found.");

        return await _repository.GetAuditAsync(tournamentId);
    }
    public async Task<TournamentParticipantState> DisqualifyTeamAsync(
        string tournamentId,
        DisqualifyTeamDto dto,
        string administratorId)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await RequirePermissionAsync(
            tournamentId,
            administratorId,
            TournamentPermission.DisqualifyTeams);

        if (string.IsNullOrWhiteSpace(dto.TeamId))
            throw new ArgumentException("TeamId is required.");

        if (string.IsNullOrWhiteSpace(dto.Reason) ||
            dto.Reason.Trim().Length < 5)
            throw new ArgumentException(
                "Reason must contain at least 5 characters.");

        string reason = dto.Reason.Trim();

        using var session = await _mongoClient.StartSessionAsync();
        session.StartTransaction();

        try
        {
            var tournament = await _tournaments
                .GetByIdAsync(session, tournamentId)
                ?? throw new KeyNotFoundException(
                    "Tournament not found.");

            if (tournament.Status != TournamentStatus.InProgress)
                throw new InvalidOperationException(
                    "Tournament must be InProgress.");

            if (!tournament.TournamentParticipants.Any(
                    t => t.Id == dto.TeamId))
                throw new ArgumentException(
                    "Team is not a tournament participant.");

            if (tournament.TournamentType is TournamentType.OlympicSystem or TournamentType.DoubleElimination)
            {
                throw new NotSupportedException(
                    "Automatic disqualification is not yet supported " +
                    "for elimination tournaments.");
            }
            var states = await _repository
                .GetParticipantStatesAsync(session, tournamentId);

            var existingState = states.FirstOrDefault(
                x => x.TeamId == dto.TeamId);

            if (existingState != null &&
                existingState.Status != TournamentParticipantStatus.Active)
                throw new InvalidOperationException(
                    "Only active participants can be disqualified.");

            var allMatches = await _matches
                .GetMatchesByTournamentIdAsync(
                    session, tournamentId);
            if (tournament.TournamentType == TournamentType.GroupStage)
            {
                if (allMatches.Count == 0 ||
                    allMatches.Any(m => m.StageId != "groups"))
                {
                    throw new InvalidOperationException(
                        "Automatic GroupStage disqualification is allowed " +
                        "only when all existing matches belong to the group stage.");
                }

                if (allMatches.Any(m =>
                    string.IsNullOrWhiteSpace(m.GroupId)))
                {
                    throw new InvalidOperationException(
                        "GroupStage contains matches without GroupId.");
                }
            }

            var otherDisqualifiedIds = states
                .Where(x =>
                    x.Status == TournamentParticipantStatus.Disqualified &&
                    x.TeamId != dto.TeamId)
                .Select(x => x.TeamId)
                .ToHashSet();

            var decisions = _disqualificationPlanner.BuildPlan(
                tournament,
                dto.TeamId,
                allMatches,
                otherDisqualifiedIds);

            bool hasUnresolvedMatch = allMatches.Any(m =>
                m.Status != MatchStatus.Completed &&
                m.Status != MatchStatus.Cancelled &&
                (m.HomeTeamId == dto.TeamId ||
                 m.AwayTeamId == dto.TeamId) &&
                (string.IsNullOrWhiteSpace(m.HomeTeamId) ||
                 string.IsNullOrWhiteSpace(m.AwayTeamId) ||
                 (m.HomeTeamId == dto.TeamId &&
                  otherDisqualifiedIds.Contains(m.AwayTeamId)) ||
                 (m.AwayTeamId == dto.TeamId &&
                  otherDisqualifiedIds.Contains(m.HomeTeamId))));

            if (hasUnresolvedMatch)
                throw new InvalidOperationException(
                    "Disqualification contains unresolved matches " +
                    "that require manual administration.");

            var requests = BuildDisqualificationRequests(
                decisions, reason);

            var now = DateTime.UtcNow;

            var state = new TournamentParticipantState
            {
                Id = existingState?.Id
                    ?? ObjectId.GenerateNewId().ToString(),
                TournamentId = tournamentId,
                TeamId = dto.TeamId,
                Status = TournamentParticipantStatus.Disqualified,
                Reason = reason,
                UpdatedBy = administratorId,
                UpdatedAt = now
            };

            if (existingState == null)
            {
                await _repository.InsertParticipantStateAsync(
                    session, state);
            }
            else
            {
                bool updated = await _repository
                    .DisqualifyParticipantAsync(session, state);

                if (!updated)
                    throw new InvalidOperationException(
                        "Participant state was modified concurrently.");
            }
            await _matchResults.SetAdministrativeResultsBatchAsync(
                session,
                requests,
                administratorId);

            await _repository.AddAuditAsync(
                session,
                new TournamentAuditEntry
                {
                    TournamentId = tournamentId,
                    ActorId = administratorId,
                    Action = "TeamDisqualified",
                    EntityType = nameof(TournamentParticipantState),
                    EntityId = state.Id,
                    Reason = reason,
                    BeforeJson = existingState == null
                        ? null
                        : JsonSerializer.Serialize(existingState),
                    AfterJson = JsonSerializer.Serialize(state),
                    CreatedAt = now
                });

            await _matchResults.FinalizeTournamentIfReadyAsync(
                session, tournamentId);

            await session.CommitTransactionAsync();

            return state;
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync();

            throw;
        }
    }

    private static List<AdministrativeMatchResultRequest>
    BuildDisqualificationRequests(
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
}
