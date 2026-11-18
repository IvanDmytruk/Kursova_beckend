using Beckend.Enums;
using Beckend.Models;
using Beckend.Services.TournamentAccess;
using MongoDB.Driver;

namespace Beckend.Services.Tournaments.Registration;

public sealed class ParticipationApprovalService
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<Tournament> _tournaments;
    private readonly IMongoCollection<TournamentParticipationRequest>
        _requests;
    private readonly IMongoCollection<Team> _teams;
    private readonly IMongoCollection<User> _users;
    private readonly ITournamentAccessService _access;

    public ParticipationApprovalService(
        IMongoClient client,
        IMongoDatabase database,
        ITournamentAccessService access)
    {
        _client = client;
        _access = access;

        _tournaments = database.GetCollection<Tournament>(
            "Tournaments");

        _requests = database
            .GetCollection<TournamentParticipationRequest>(
                "TournamentParticipationRequests");

        _teams = database.GetCollection<Team>("Teams");
        _users = database.GetCollection<User>("Users");
    }

    public async Task ApproveAsync(
        string requestId,
        string actorId,
        Func<TournamentParticipationRequest, string, Task<bool>>
            canAcceptInvitation,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            throw new UnauthorizedAccessException();

        var initialRequest = await _requests
            .Find(x => x.Id == requestId)
            .FirstOrDefaultAsync(ct);

        if (initialRequest == null)
            throw new KeyNotFoundException(
                "Participation request not found");

        bool allowed;

        if (initialRequest.Type ==
            ParticipationRequestType.Application)
        {
            allowed = await _access.HasPermissionAsync(
                initialRequest.TournamentId,
                actorId,
                TournamentPermission.ManageParticipants);
        }
        else
        {
            allowed = await canAcceptInvitation(
                initialRequest, actorId);
        }

        if (!allowed)
            throw new UnauthorizedAccessException(
                "Insufficient permissions");

        using var session = await _client
            .StartSessionAsync(cancellationToken: ct);

        await session.WithTransactionAsync(
            async (s, token) =>
            {
                var request = await _requests
                    .Find(s, x => x.Id == requestId)
                    .FirstOrDefaultAsync(token);

                if (request == null)
                    throw new KeyNotFoundException(
                        "Request not found");

                if (request.Status !=
                    ParticipationRequestStatus.Pending)
                    throw new InvalidOperationException(
                        "Request has already been processed");

                if (request.Type != initialRequest.Type ||
                    request.TournamentId !=
                        initialRequest.TournamentId ||
                    request.TeamId != initialRequest.TeamId ||
                    request.PlayerId != initialRequest.PlayerId)
                {
                    throw new InvalidOperationException(
                        "Request target changed");
                }

                var tournament = await _tournaments
                    .Find(s, x => x.Id == request.TournamentId)
                    .FirstOrDefaultAsync(token);

                if (tournament == null)
                    throw new KeyNotFoundException(
                        "Tournament not found");

                if (tournament.Status !=
                    TournamentStatus.Registration)
                {
                    throw new InvalidOperationException(
                        "Registration is closed");
                }

                if (tournament.MaxParticipants < 0)
                    throw new InvalidOperationException(
                        "Invalid participant limit");

                if (tournament.MaxParticipants > 0 &&
                    tournament.TournamentParticipants.Count >=
                    tournament.MaxParticipants)
                {
                    throw new InvalidOperationException(
                        "Tournament is full");
                }

                Team participant;

                if (tournament.Format ==
                    TournamentFormat.Command)
                {
                    if (string.IsNullOrWhiteSpace(request.TeamId) ||
                        !string.IsNullOrWhiteSpace(request.PlayerId))
                    {
                        throw new InvalidOperationException(
                            "Invalid team registration request");
                    }

                    participant = await _teams
                        .Find(s, x =>
                            x.Id == request.TeamId &&
                            !x.IsArchived)
                        .FirstOrDefaultAsync(token)
                        ?? throw new KeyNotFoundException(
                            "Team not found or archived");

                    if (participant.SportName !=
                        tournament.SportName)
                    {
                        throw new InvalidOperationException(
                            "Team sport does not match tournament");
                    }
                }
                else if (tournament.Format ==
                         TournamentFormat.Individual)
                {
                    if (string.IsNullOrWhiteSpace(request.PlayerId) ||
                        !string.IsNullOrWhiteSpace(request.TeamId))
                    {
                        throw new InvalidOperationException(
                            "Invalid player registration request");
                    }

                    var player = await _users
                        .Find(s, x => x.Id == request.PlayerId)
                        .FirstOrDefaultAsync(token)
                        ?? throw new KeyNotFoundException(
                            "Player not found");

                    if (player.IsBanned &&
                        (!player.BannedUntil.HasValue ||
                         player.BannedUntil.Value > DateTime.UtcNow))
                    {
                        throw new InvalidOperationException(
                            "Player is banned");
                    }

                    participant = new Team
                    {
                        Id = player.Id,
                        TeamName =
                            $"{player.Name} {player.Surname}".Trim(),
                        TeamDescription = "Individual participant",
                        SportName = tournament.SportName
                    };
                }
                else
                {
                    throw new InvalidOperationException(
                        "Unsupported tournament format");
                }

                if (tournament.TournamentParticipants.Any(
                    x => x.Id == participant.Id))
                {
                    throw new InvalidOperationException(
                        "Participant already registered");
                }
                var tournamentResult = await _tournaments
                    .UpdateOneAsync(
                        s,
                        x => x.Id == tournament.Id &&
                             x.Status ==
                                 TournamentStatus.Registration &&
                             !x.TournamentParticipants.Any(
                                 p => p.Id == participant.Id),
                        Builders<Tournament>.Update.Push(
                            x => x.TournamentParticipants,
                            participant),
                        cancellationToken: token);

                if (tournamentResult.ModifiedCount != 1)
                {
                    throw new InvalidOperationException(
                        "Tournament registration conflict");
                }

                var requestResult = await _requests.UpdateOneAsync(
                    s,
                    x => x.Id == requestId &&
                         x.Status ==
                             ParticipationRequestStatus.Pending,
                    Builders<TournamentParticipationRequest>
                        .Update
                        .Set(
                            x => x.Status,
                            ParticipationRequestStatus.Accepted)
                        .Set(x => x.ReviewedByUserId, actorId)
                        .Set(x => x.ReviewedAt, DateTime.UtcNow),
                    cancellationToken: token);

                if (requestResult.ModifiedCount != 1)
                {
                    throw new InvalidOperationException(
                        "Participation request conflict");
                }

                return true;
            },
            cancellationToken: ct);
    }
}
