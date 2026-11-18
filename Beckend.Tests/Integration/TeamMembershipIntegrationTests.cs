
using Beckend.Enums;
using Beckend.Models;
using Beckend.Services.Teams;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Beckend.Tests.Integration;

public sealed class TeamMembershipIntegrationTests
    : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;

    private readonly IMongoCollection<Team> _teams;
    private readonly IMongoCollection<TeamMember> _members;
    private readonly IMongoCollection<TeamInvitation> _invitations;

    private readonly TeamInvitationAcceptanceService _service;
    private readonly TeamWorkflowIndexes _indexes;

    public TeamMembershipIntegrationTests(
        MongoDbFixture fixture)
    {
        _fixture = fixture;

        _teams = fixture.Database
            .GetCollection<Team>("Teams");

        _members = fixture.Database
            .GetCollection<TeamMember>("TeamMembers");

        _invitations = fixture.Database
            .GetCollection<TeamInvitation>("TeamInvitations");

        _service = new TeamInvitationAcceptanceService(
            fixture.Database.Client,
            fixture.Database);

        _indexes = new TeamWorkflowIndexes(
            fixture.Database);
    }

    private static string NewId() =>
        ObjectId.GenerateNewId().ToString();

    private async Task<(Team Team, TeamInvitation Invitation)>
        SeedInvitationAsync(
            TeamRole role = TeamRole.Player)
    {
        var ownerId = NewId();
        var invitedUserId = NewId();

        var team = new Team
        {
            TeamName = $"TestTeam-{Guid.NewGuid():N}",
            TeamDescription = "Integration test team",
            OwnerId = ownerId,
            SportName = SportName.Contr_Strike_2,
            IsArchived = false
        };

        await _teams.InsertOneAsync(team);

        var invitation = new TeamInvitation
        {
            TeamId = team.Id,
            UserId = invitedUserId,
            CreatedByUserId = ownerId,
            Role = role,
            Status = TeamInvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _invitations.InsertOneAsync(invitation);

        return (team, invitation);
    }

    [Fact]
    public async Task AcceptInvitation_ValidInvitation_CreatesMembership()
    {
        // Arrange
        var (team, invitation) = await SeedInvitationAsync();

        // Act
        await _service.AcceptAsync(
            invitation.Id,
            invitation.UserId);

        // Assert
        var savedInvitation = await _invitations.Find(
            x => x.Id == invitation.Id)
            .FirstOrDefaultAsync();

        Assert.NotNull(savedInvitation);
        Assert.Equal(
            TeamInvitationStatus.Accepted,
            savedInvitation.Status);

        Assert.NotNull(savedInvitation.RespondedAt);

        var memberships = await _members.Find(x =>
            x.TeamId == team.Id &&
            x.UserId == invitation.UserId &&
            x.IsActive)
            .ToListAsync();

        var membership = Assert.Single(memberships);

        Assert.Equal(TeamRole.Player, membership.Role);
        Assert.Null(membership.LeftAt);
        Assert.True(membership.JoinedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task AcceptInvitation_Twice_DoesNotCreateDuplicate()
    {
        // Arrange
        var (team, invitation) = await SeedInvitationAsync();

        await _service.AcceptAsync(
            invitation.Id,
            invitation.UserId);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AcceptAsync(
                invitation.Id,
                invitation.UserId));

        // Assert
        var count = await _members.CountDocumentsAsync(x =>
            x.TeamId == team.Id &&
            x.UserId == invitation.UserId &&
            x.IsActive);

        Assert.Equal(1, count);

        var savedInvitation = await _invitations.Find(
            x => x.Id == invitation.Id)
            .FirstOrDefaultAsync();

        Assert.Equal(
            TeamInvitationStatus.Accepted,
            savedInvitation.Status);
    }

    [Fact]
    public async Task AcceptInvitation_OtherUser_CannotAccept()
    {
        // Arrange
        var (team, invitation) = await SeedInvitationAsync();

        var anotherUserId = NewId();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AcceptAsync(
                invitation.Id,
                anotherUserId));

        // Assert
        var savedInvitation = await _invitations.Find(
            x => x.Id == invitation.Id)
            .FirstOrDefaultAsync();

        Assert.Equal(
            TeamInvitationStatus.Pending,
            savedInvitation.Status);

        var count = await _members.CountDocumentsAsync(x =>
            x.TeamId == team.Id &&
            x.UserId == anotherUserId);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task AcceptInvitation_ArchivedTeam_IsRejected()
    {
        // Arrange
        var (team, invitation) = await SeedInvitationAsync();

        await _teams.UpdateOneAsync(
            x => x.Id == team.Id,
            Builders<Team>.Update
                .Set(x => x.IsArchived, true));

        // Act
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.AcceptAsync(
                invitation.Id,
                invitation.UserId));

        // Assert
        var count = await _members.CountDocumentsAsync(x =>
            x.TeamId == team.Id &&
            x.UserId == invitation.UserId &&
            x.IsActive);

        Assert.Equal(0, count);

        var savedInvitation = await _invitations.Find(
            x => x.Id == invitation.Id)
            .FirstOrDefaultAsync();

        Assert.Equal(
            TeamInvitationStatus.Pending,
            savedInvitation.Status);
    }

    [Fact]
    public async Task RejoinTeam_PreservesPreviousMembershipHistory()
    {
        // Arrange
        var (team, invitation) = await SeedInvitationAsync();

        var previousMembership = new TeamMember
        {
            TeamId = team.Id,
            UserId = invitation.UserId,
            Role = TeamRole.Player,
            JoinedAt = DateTime.UtcNow.AddMonths(-2),
            LeftAt = DateTime.UtcNow.AddMonths(-1),
            IsActive = false
        };

        await _members.InsertOneAsync(previousMembership);

        // Act
        await _service.AcceptAsync(
            invitation.Id,
            invitation.UserId);

        // Assert
        var history = await _members.Find(x =>
            x.TeamId == team.Id &&
            x.UserId == invitation.UserId)
            .ToListAsync();

        Assert.Equal(2, history.Count);
        Assert.Single(history, x => x.IsActive);
        Assert.Single(history, x => !x.IsActive);

        var oldRecord = history.Single(x =>
            x.Id == previousMembership.Id);

        Assert.False(oldRecord.IsActive);
        Assert.NotNull(oldRecord.LeftAt);

        var newRecord = history.Single(x => x.IsActive);

        Assert.NotEqual(
            previousMembership.Id,
            newRecord.Id);
    }

    [Fact]
    public async Task AcceptInvitation_AlreadyActiveMember_IsRejected()
    {
        // Arrange
        var (team, invitation) = await SeedInvitationAsync();

        await _members.InsertOneAsync(new TeamMember
        {
            TeamId = team.Id,
            UserId = invitation.UserId,
            Role = TeamRole.Player,
            IsActive = true
        });

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AcceptAsync(
                invitation.Id,
                invitation.UserId));

        // Assert
        var count = await _members.CountDocumentsAsync(x =>
            x.TeamId == team.Id &&
            x.UserId == invitation.UserId &&
            x.IsActive);

        Assert.Equal(1, count);

        var savedInvitation = await _invitations.Find(
            x => x.Id == invitation.Id)
            .FirstOrDefaultAsync();

        Assert.Equal(
            TeamInvitationStatus.Pending,
            savedInvitation.Status);
    }

    [Fact]
    public async Task AcceptInvitation_ConcurrentRequests_OnlyOneSucceeds()
    {
        // Arrange
        await _indexes.EnsureAsync();

        var (team, invitation) = await SeedInvitationAsync();

        // Act
        var requests = new[]
        {
            TryAcceptAsync(invitation.Id, invitation.UserId),
            TryAcceptAsync(invitation.Id, invitation.UserId)
        };

        var results = await Task.WhenAll(requests);

        // Assert
        Assert.Equal(1, results.Count(x => x));

        var memberships = await _members.Find(x =>
            x.TeamId == team.Id &&
            x.UserId == invitation.UserId &&
            x.IsActive)
            .ToListAsync();

        Assert.Single(memberships);

        var savedInvitation = await _invitations.Find(
            x => x.Id == invitation.Id)
            .FirstOrDefaultAsync();

        Assert.Equal(
            TeamInvitationStatus.Accepted,
            savedInvitation.Status);
    }

    private async Task<bool> TryAcceptAsync(
        string invitationId,
        string userId)
    {
        try
        {
            await _service.AcceptAsync(
                invitationId,
                userId);

            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (MongoException)
        {
            // Конкурентний запис може спричинити
            // транзакційний конфлікт MongoDB.
            return false;
        }
    }
}
