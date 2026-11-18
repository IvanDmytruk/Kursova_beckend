using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Services.Teams;

public sealed class TeamWorkflowIndexes
{
    private readonly IMongoDatabase _database;

    public TeamWorkflowIndexes(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task EnsureAsync(
        CancellationToken ct = default)
    {
        var members = _database
            .GetCollection<TeamMember>("TeamMembers");

        var invitations = _database
            .GetCollection<TeamInvitation>("TeamInvitations");

        var requests = _database
            .GetCollection<TournamentParticipationRequest>(
                "TournamentParticipationRequests");
        var teams = _database.GetCollection<Team>("Teams");

        await teams.Indexes.CreateOneAsync(
            new CreateIndexModel<Team>(
                Builders<Team>.IndexKeys.Ascending(
                    x => x.TeamName),
                new CreateIndexOptions<Team>
                {
                    Name = "ux_active_team_name",
                    Unique = true,
                    PartialFilterExpression =
                        Builders<Team>.Filter.Eq(
                            x => x.IsArchived, false)
                }),
            cancellationToken: ct);

        await members.Indexes.CreateOneAsync(
            new CreateIndexModel<TeamMember>(
                Builders<TeamMember>.IndexKeys
                    .Ascending(x => x.TeamId)
                    .Ascending(x => x.UserId),
                new CreateIndexOptions<TeamMember>
                {
                    Name = "ux_active_team_membership",
                    Unique = true,
                    PartialFilterExpression =
                        Builders<TeamMember>.Filter.Eq(
                            x => x.IsActive, true)
                }),
            cancellationToken: ct);

        await invitations.Indexes.CreateOneAsync(
            new CreateIndexModel<TeamInvitation>(
                Builders<TeamInvitation>.IndexKeys
                    .Ascending(x => x.TeamId)
                    .Ascending(x => x.UserId),
                new CreateIndexOptions<TeamInvitation>
                {
                    Name = "ux_pending_team_invitation",
                    Unique = true,
                    PartialFilterExpression =
                        Builders<TeamInvitation>.Filter.Eq(
                            x => x.Status,
                            TeamInvitationStatus.Pending)
                }),
            cancellationToken: ct);

        await requests.Indexes.CreateOneAsync(
            new CreateIndexModel<TournamentParticipationRequest>(
                Builders<TournamentParticipationRequest>.IndexKeys
                    .Ascending(x => x.TournamentId)
                    .Ascending(x => x.TeamId)
                    .Ascending(x => x.PlayerId),
                new CreateIndexOptions<TournamentParticipationRequest>
                {
                    Name = "ux_pending_tournament_participation",
                    Unique = true,
                    PartialFilterExpression =
                        Builders<TournamentParticipationRequest>
                            .Filter.Eq(
                                x => x.Status,
                                ParticipationRequestStatus.Pending)
                }),
            cancellationToken: ct);

        await requests.Indexes.CreateOneAsync(
            new CreateIndexModel<TournamentParticipationRequest>(
                Builders<TournamentParticipationRequest>.IndexKeys
                    .Ascending(x => x.CreatedByUserId)
                    .Descending(x => x.CreatedAt)),
            cancellationToken: ct);

        await members.Indexes.CreateOneAsync(
            new CreateIndexModel<TeamMember>(
                Builders<TeamMember>.IndexKeys
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.IsActive)),
            cancellationToken: ct);
    }
}
