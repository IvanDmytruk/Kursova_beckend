
using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Services.Teams;

public sealed class TeamInvitationAcceptanceService
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<Team> _teams;
    private readonly IMongoCollection<TeamMember> _members;
    private readonly IMongoCollection<TeamInvitation> _invitations;

    public TeamInvitationAcceptanceService(
        IMongoClient client,
        IMongoDatabase database)
    {
        _client = client;

        _teams = database.GetCollection<Team>("Teams");
        _members = database.GetCollection<TeamMember>(
            "TeamMembers");
        _invitations = database.GetCollection<TeamInvitation>(
            "TeamInvitations");
    }

    public async Task AcceptAsync(
        string invitationId,
        string actorId,
        CancellationToken ct = default)
    {
        using var session = await _client
            .StartSessionAsync(cancellationToken: ct);

        await session.WithTransactionAsync(
            async (s, token) =>
            {
                var invitation = await _invitations
                    .Find(s, x =>
                        x.Id == invitationId &&
                        x.UserId == actorId &&
                        x.Status == TeamInvitationStatus.Pending)
                    .FirstOrDefaultAsync(token);

                if (invitation == null)
                {
                    throw new InvalidOperationException(
                        "Invitation not found or already processed");
                }

                var team = await _teams
                    .Find(s, x =>
                        x.Id == invitation.TeamId &&
                        !x.IsArchived)
                    .FirstOrDefaultAsync(token);

                if (team == null)
                    throw new KeyNotFoundException(
                        "Team not found or archived");

                var existingMember = await _members
                    .Find(s, x =>
                        x.TeamId == invitation.TeamId &&
                        x.UserId == actorId &&
                        x.IsActive)
                    .AnyAsync(token);

                if (existingMember)
                    throw new InvalidOperationException(
                        "User is already a member");

                var now = DateTime.UtcNow;

                var membership = new TeamMember
                {
                    TeamId = invitation.TeamId,
                    UserId = actorId,
                    Role = invitation.Role,
                    JoinedAt = now,
                    IsActive = true
                };

                await _members.InsertOneAsync(
                    s, membership,
                    cancellationToken: token);

                var result = await _invitations.UpdateOneAsync(
                    s,
                    x => x.Id == invitationId &&
                         x.UserId == actorId &&
                         x.Status == TeamInvitationStatus.Pending,
                    Builders<TeamInvitation>.Update
                        .Set(
                            x => x.Status,
                            TeamInvitationStatus.Accepted)
                        .Set(x => x.RespondedAt, now),
                    cancellationToken: token);

                if (result.ModifiedCount != 1)
                {
                    throw new InvalidOperationException(
                        "Invitation was processed concurrently");
                }

                return true;
            },
            cancellationToken: ct);
    }
}
