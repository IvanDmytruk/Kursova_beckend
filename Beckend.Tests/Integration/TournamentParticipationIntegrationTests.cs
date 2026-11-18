
using Beckend.Enums;
using Beckend.Models;
using Beckend.Services.TournamentAccess;
using Beckend.Services.Tournaments.Registration;
using Moq;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Beckend.Tests.Integration;

public sealed class TournamentParticipationIntegrationTests
    : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;

    private readonly IMongoCollection<Tournament> _tournaments;
    private readonly IMongoCollection<Team> _teams;
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<TournamentParticipationRequest>
        _requests;

    private readonly Mock<ITournamentAccessService> _access = new();
    private readonly ParticipationApprovalService _service;

    public TournamentParticipationIntegrationTests(
        MongoDbFixture fixture)
    {
        _fixture = fixture;

        _tournaments = fixture.Database
            .GetCollection<Tournament>("Tournaments");

        _teams = fixture.Database
            .GetCollection<Team>("Teams");

        _users = fixture.Database
            .GetCollection<User>("Users");

        _requests = fixture.Database
            .GetCollection<TournamentParticipationRequest>(
                "TournamentParticipationRequests");

        _access.Setup(x => x.HasPermissionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                TournamentPermission.ManageParticipants))
            .ReturnsAsync(true);

        _service = new ParticipationApprovalService(
            fixture.Database.Client,
            fixture.Database,
            _access.Object);
    }

    private static string NewId() =>
        ObjectId.GenerateNewId().ToString();

    private async Task<Tournament> CreateTournamentAsync(
        TournamentFormat format = TournamentFormat.Command,
        int maxParticipants = 4,
        TournamentStatus status = TournamentStatus.Registration)
    {
        var tournament = new Tournament
        {
            TournamentName = $"Test-{Guid.NewGuid():N}",
            TournamentType = TournamentType.RoundRobin,
            Format = format,
            SportName = SportName.Contr_Strike_2,
            Status = status,
            CreatedBy = NewId(),
            MaxParticipants = maxParticipants,
            StartDate = DateTime.UtcNow.AddDays(7),
            TournamentParticipants = new List<Team>()
        };

        await _tournaments.InsertOneAsync(tournament);

        return tournament;
    }

    private async Task<Team> CreateTeamAsync(
        SportName sport = SportName.Contr_Strike_2,
        bool archived = false)
    {
        var team = new Team
        {
            TeamName = $"Team-{Guid.NewGuid():N}",
            SportName = sport,
            OwnerId = NewId(),
            IsArchived = archived
        };

        await _teams.InsertOneAsync(team);
        return team;
    }

    private async Task<TournamentParticipationRequest>
        CreateRequestAsync(
            Tournament tournament,
            string? teamId = null,
            string? playerId = null,
            ParticipationRequestType type =
                ParticipationRequestType.Application)
    {
        var request = new TournamentParticipationRequest
        {
            TournamentId = tournament.Id,
            TeamId = teamId ?? "",
            PlayerId = playerId ?? "",
            Type = type,
            Status = ParticipationRequestStatus.Pending,
            CreatedByUserId = NewId()
        };

        await _requests.InsertOneAsync(request);
        return request;
    }

    private Task ApproveAsync(
        TournamentParticipationRequest request,
        string actorId,
        bool targetCanAccept = true)
    {
        return _service.ApproveAsync(
            request.Id,
            actorId,
            (_, _) => Task.FromResult(targetCanAccept));
    }

    private async Task<Tournament> ReloadTournamentAsync(
        string id)
    {
        return (await _tournaments
            .Find(t => t.Id == id)
            .FirstOrDefaultAsync())!;
    }

    private async Task<TournamentParticipationRequest>
        ReloadRequestAsync(string id)
    {
        return (await _requests
            .Find(r => r.Id == id)
            .FirstOrDefaultAsync())!;
    }

    [Fact]
    public async Task ApproveTeamApplication_AddsParticipant()
    {
        var tournament = await CreateTournamentAsync();
        var team = await CreateTeamAsync();

        var request = await CreateRequestAsync(
            tournament, teamId: team.Id);

        await ApproveAsync(request, tournament.CreatedBy);

        var saved = await ReloadTournamentAsync(tournament.Id);
        var updated = await ReloadRequestAsync(request.Id);

        var participant = Assert.Single(
            saved.TournamentParticipants);

        Assert.Equal(team.Id, participant.Id);
        Assert.Equal(team.TeamName, participant.TeamName);
        Assert.Equal(
            ParticipationRequestStatus.Accepted,
            updated.Status);
        Assert.Equal(
            tournament.CreatedBy,
            updated.ReviewedByUserId);
        Assert.NotNull(updated.ReviewedAt);
    }

    [Fact]
    public async Task ApproveInvitation_WithConsent_AddsTeam()
    {
        var tournament = await CreateTournamentAsync();
        var team = await CreateTeamAsync();

        var request = await CreateRequestAsync(
            tournament,
            teamId: team.Id,
            type: ParticipationRequestType.Invitation);

        await ApproveAsync(
            request,
            team.OwnerId,
            targetCanAccept: true);

        var saved = await ReloadTournamentAsync(tournament.Id);
        var updated = await ReloadRequestAsync(request.Id);

        Assert.Contains(
            saved.TournamentParticipants,
            x => x.Id == team.Id);

        Assert.Equal(
            ParticipationRequestStatus.Accepted,
            updated.Status);
    }

    [Fact]
    public async Task ApproveInvitation_WithoutConsent_IsRejected()
    {
        var tournament = await CreateTournamentAsync();
        var team = await CreateTeamAsync();

        var request = await CreateRequestAsync(
            tournament,
            teamId: team.Id,
            type: ParticipationRequestType.Invitation);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => ApproveAsync(
                request,
                NewId(),
                targetCanAccept: false));

        var saved = await ReloadTournamentAsync(tournament.Id);
        var updated = await ReloadRequestAsync(request.Id);

        Assert.Empty(saved.TournamentParticipants);
        Assert.Equal(
            ParticipationRequestStatus.Pending,
            updated.Status);
    }

    [Fact]
    public async Task ApproveApplication_WithoutManagerPermission_IsRejected()
    {
        var tournament = await CreateTournamentAsync();
        var team = await CreateTeamAsync();
        var request = await CreateRequestAsync(
            tournament, teamId: team.Id);

        _access.Setup(x => x.HasPermissionAsync(
                tournament.Id,
                It.IsAny<string>(),
                TournamentPermission.ManageParticipants))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => ApproveAsync(request, NewId()));

        var saved = await ReloadTournamentAsync(tournament.Id);
        var updated = await ReloadRequestAsync(request.Id);

        Assert.Empty(saved.TournamentParticipants);
        Assert.Equal(
            ParticipationRequestStatus.Pending,
            updated.Status);
    }

    [Fact]
    public async Task ApproveApplication_Twice_DoesNotDuplicateParticipant()
    {
        var tournament = await CreateTournamentAsync();
        var team = await CreateTeamAsync();

        var request = await CreateRequestAsync(
            tournament, teamId: team.Id);

        await ApproveAsync(request, tournament.CreatedBy);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ApproveAsync(
                request, tournament.CreatedBy));

        var saved = await ReloadTournamentAsync(tournament.Id);

        Assert.Single(saved.TournamentParticipants);
    }

    [Fact]
    public async Task ApproveApplication_ClosedRegistration_IsRejected()
    {
        var tournament = await CreateTournamentAsync(
            status: TournamentStatus.Ready);

        var team = await CreateTeamAsync();
        var request = await CreateRequestAsync(
            tournament, teamId: team.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ApproveAsync(
                request, tournament.CreatedBy));

        var saved = await ReloadTournamentAsync(tournament.Id);
        var updated = await ReloadRequestAsync(request.Id);

        Assert.Empty(saved.TournamentParticipants);
        Assert.Equal(
            ParticipationRequestStatus.Pending,
            updated.Status);
    }

    [Fact]
    public async Task ApproveApplication_WrongSport_IsRejected()
    {
        var tournament = await CreateTournamentAsync();

        var team = await CreateTeamAsync(
            SportName.Football);

        var request = await CreateRequestAsync(
            tournament, teamId: team.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ApproveAsync(
                request, tournament.CreatedBy));

        var saved = await ReloadTournamentAsync(tournament.Id);
        var updated = await ReloadRequestAsync(request.Id);

        Assert.Empty(saved.TournamentParticipants);
        Assert.Equal(
            ParticipationRequestStatus.Pending,
            updated.Status);
    }

    [Fact]
    public async Task ApproveApplication_ArchivedTeam_IsRejected()
    {
        var tournament = await CreateTournamentAsync();
        var team = await CreateTeamAsync(archived: true);

        var request = await CreateRequestAsync(
            tournament, teamId: team.Id);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => ApproveAsync(
                request, tournament.CreatedBy));

        var saved = await ReloadTournamentAsync(tournament.Id);

        Assert.Empty(saved.TournamentParticipants);
    }

    [Fact]
    public async Task ApproveApplication_FullTournament_IsRejected()
    {
        var tournament = await CreateTournamentAsync(
            maxParticipants: 1);

        var existingTeam = await CreateTeamAsync();
        var newTeam = await CreateTeamAsync();

        await _tournaments.UpdateOneAsync(
            t => t.Id == tournament.Id,
            Builders<Tournament>.Update.Push(
                t => t.TournamentParticipants,
                existingTeam));

        var request = await CreateRequestAsync(
            tournament, teamId: newTeam.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ApproveAsync(
                request, tournament.CreatedBy));

        var saved = await ReloadTournamentAsync(tournament.Id);
        var updated = await ReloadRequestAsync(request.Id);

        Assert.Single(saved.TournamentParticipants);
        Assert.Equal(
            ParticipationRequestStatus.Pending,
            updated.Status);
    }

    [Fact]
    public async Task ApproveApplication_AlreadyRegistered_IsRejected()
    {
        var tournament = await CreateTournamentAsync();
        var team = await CreateTeamAsync();

        await _tournaments.UpdateOneAsync(
            t => t.Id == tournament.Id,
            Builders<Tournament>.Update.Push(
                t => t.TournamentParticipants,
                team));

        var request = await CreateRequestAsync(
            tournament, teamId: team.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ApproveAsync(
                request, tournament.CreatedBy));

        var saved = await ReloadTournamentAsync(tournament.Id);
        var updated = await ReloadRequestAsync(request.Id);

        Assert.Single(saved.TournamentParticipants);
        Assert.Equal(
            ParticipationRequestStatus.Pending,
            updated.Status);
    }

    [Fact]
    public async Task ApproveIndividualApplication_AddsPlayer()
    {
        var tournament = await CreateTournamentAsync(
            format: TournamentFormat.Individual);

        var player = new User
        {
            Name = "Test",
            Surname = "Player",
            Age = 19
        };

        await _users.InsertOneAsync(player);

        var request = await CreateRequestAsync(
            tournament, playerId: player.Id);

        await ApproveAsync(request, tournament.CreatedBy);

        var saved = await ReloadTournamentAsync(tournament.Id);
        var updated = await ReloadRequestAsync(request.Id);

        Assert.Single(saved.TournamentParticipants);
        Assert.Equal(
            player.Id,
            saved.TournamentParticipants[0].Id);
        Assert.Equal(
            ParticipationRequestStatus.Accepted,
            updated.Status);
    }

    [Fact]
    public async Task ApproveIndividualApplication_BannedPlayer_IsRejected()
    {
        var tournament = await CreateTournamentAsync(
            format: TournamentFormat.Individual);

        var player = new User
        {
            Name = "Banned",
            Surname = "Player",
            Age = 20,
            IsBanned = true,
            BannedUntil = DateTime.UtcNow.AddDays(2)
        };

        await _users.InsertOneAsync(player);

        var request = await CreateRequestAsync(
            tournament, playerId: player.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ApproveAsync(
                request, tournament.CreatedBy));

        var saved = await ReloadTournamentAsync(tournament.Id);
        var updated = await ReloadRequestAsync(request.Id);

        Assert.Empty(saved.TournamentParticipants);
        Assert.Equal(
            ParticipationRequestStatus.Pending,
            updated.Status);
    }

    [Fact]
    public async Task ConcurrentApprovals_LastPlace_OnlyOneSucceeds()
    {
        var tournament = await CreateTournamentAsync(
            maxParticipants: 1);

        var teamA = await CreateTeamAsync();
        var teamB = await CreateTeamAsync();

        var requestA = await CreateRequestAsync(
            tournament, teamId: teamA.Id);

        var requestB = await CreateRequestAsync(
            tournament, teamId: teamB.Id);

        var start = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Exception?> Attempt(
            TournamentParticipationRequest request)
        {
            await start.Task;

            try
            {
                await ApproveAsync(
                    request, tournament.CreatedBy);

                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var first = Task.Run(() => Attempt(requestA));
        var second = Task.Run(() => Attempt(requestB));

        start.SetResult(true);

        var results = await Task.WhenAll(first, second);

        var successful = results.Count(x => x is null);

        var saved = await ReloadTournamentAsync(tournament.Id);

        var updatedA = await ReloadRequestAsync(requestA.Id);
        var updatedB = await ReloadRequestAsync(requestB.Id);

        Assert.Equal(1, successful);
        Assert.Single(saved.TournamentParticipants);

        var accepted = new[] { updatedA, updatedB }
            .Count(r => r.Status ==
                ParticipationRequestStatus.Accepted);

        Assert.Equal(1, accepted);

        var acceptedRequest = updatedA.Status ==
            ParticipationRequestStatus.Accepted
            ? updatedA
            : updatedB;

        Assert.Equal(
            saved.TournamentParticipants[0].Id,
            acceptedRequest.TeamId);
    }
}
