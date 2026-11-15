using AutoMapper;
using Beckend.DTOs;
using Beckend.DTOs.TournamentAdministration;
using Beckend.DTOs.Match;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Swiss;
using Beckend.Repositories.Teams;
using Beckend.Repositories.TournamentAdministration;
using Beckend.Repositories.Tournaments;
using Beckend.Repositories.TournamentStaffs;
using Beckend.Services.Matches.Results;
using Beckend.Services.TournamentAccess;
using Beckend.Services.TournamentAdministration;
using Beckend.Services.TournamentGenerators;
using MongoDB.Driver;
using Moq;
using Xunit;

using Match = Beckend.Models.Match;

namespace Beckend.Tests.Integration;

public class TournamentDisqualificationIntegrationTests : IClassFixture<MongoDbFixture>
{
    private const string AdministratorId = "507f1f77bcf86cd799439015";
    private readonly MongoDbFixture _fixture;
    private readonly TournamentAdministrationService _service;
    private readonly TournamentAdministrationRepository _administration;
    private readonly MatchRepository _matches;

    public TournamentDisqualificationIntegrationTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        var tournaments = new TournamentRepository(fixture.Database);
        _matches = new MatchRepository(fixture.Database);
        _administration = new TournamentAdministrationRepository(fixture.Database);
        var byes = new SwissByeRepository(fixture.Database);
        var staff = new TournamentStaffRepository(fixture.Database);
        var access = new TournamentAccessService(tournaments, staff);
        var mapper = new Mock<IMapper>();
        mapper.Setup(x => x.Map<MatchDto>(It.IsAny<object>())).Returns(new MatchDto());
        var results = new MatchResultService(
            _matches, tournaments, byes, _administration,
            fixture.Database.Client, mapper.Object,
            new DoubleEliminationGenerator());
        _service = new TournamentAdministrationService(
            _administration, tournaments, new TeamRepository(fixture.Database),
            access, fixture.Database.Client, fixture.Database,
            _matches, results, new DisqualificationPlanner());
    }

    private async Task<(Tournament Tournament, Team[] Teams, Match[] Matches)> SeedAsync(
        bool completedMatch = false)
    {
        var teams = Enumerable.Range(1, 4)
            .Select(i => new Team { TeamName = $"DQ-{Guid.NewGuid():N}-{i}" })
            .ToArray();
        var tournament = new Tournament
        {
            TournamentName = $"DQ integration {Guid.NewGuid():N}",
            CreatedBy = AdministratorId,
            TournamentType = TournamentType.RoundRobin,
            Status = TournamentStatus.InProgress,
            TournamentParticipants = teams.ToList(),
            Settings = new TournamentSettings { AllowDraws = false }
        };
        await _fixture.Database.GetCollection<Tournament>("Tournaments")
            .InsertOneAsync(tournament);

        Match Create(Team home, Team away, bool completed = false) => new()
        {
            TournamentId = tournament.Id,
            HomeTeamId = home.Id,
            AwayTeamId = away.Id,
            RoundNumber = 1,
            Status = completed ? MatchStatus.Completed : MatchStatus.Planing,
            HomeScore = completed ? 2 : null,
            AwayScore = completed ? 0 : null,
            WinnerId = completed ? home.Id : null
        };
        var matches = new[]
        {
            Create(teams[0], teams[1], completedMatch),
            Create(teams[2], teams[0]),
            Create(teams[1], teams[3]) // unrelated unfinished match prevents tournament finalization
        };
        await _fixture.Database.GetCollection<Match>("Matches")
            .InsertManyAsync(matches);
        return (tournament, teams, matches);
    }
    private async Task<(Tournament Tournament, Team[] Teams, Match[] Matches)>
    SeedGroupStageAsync(
        bool completedMatch = false,
        bool includePlayoff = false)
{
    var teams = Enumerable.Range(1, 4)
        .Select(i => new Team
        {
            TeamName = $"Group-DQ-{Guid.NewGuid():N}-{i}"
        })
        .ToArray();

    var tournament = new Tournament
    {
        TournamentName = $"Group DQ {Guid.NewGuid():N}",
        CreatedBy = AdministratorId,
        TournamentType = TournamentType.GroupStage,
        Status = TournamentStatus.InProgress,
        TournamentParticipants = teams.ToList(),
        Settings = new TournamentSettings
        {
            GroupsCount = 2,
            ParticipantsAdvancing = 2,
            MatchesPerPair = 1,
            AllowDraws = true,
            PointsForWin = 3,
            PointsForDraw = 1,
            PointsForLoss = 0
        }
    };

    await _fixture.Database
        .GetCollection<Tournament>("Tournaments")
        .InsertOneAsync(tournament);

    Match Create(
        Team home,
        Team away,
        string stageId,
        string? groupId,
        bool completed = false) => new()
    {
        TournamentId = tournament.Id,
        HomeTeamId = home.Id,
        AwayTeamId = away.Id,
        StageId = stageId,
        GroupId = groupId,
        RoundNumber = 1,
        Status = completed
            ? MatchStatus.Completed
            : MatchStatus.Planing,
        HomeScore = completed ? 2 : null,
        AwayScore = completed ? 0 : null,
        WinnerId = completed ? home.Id : null
    };

    var matches = new List<Match>
    {
        Create(teams[0], teams[1], "groups", "group-1", completedMatch),
        Create(teams[2], teams[0], "groups", "group-1"),
        Create(teams[1], teams[3], "groups", "group-2")
    };

    if (includePlayoff)
    {
        matches.Add(Create(
            teams[1], teams[2], "playoff", null));
    }

    await _fixture.Database
        .GetCollection<Match>("Matches")
        .InsertManyAsync(matches);

    return (tournament, teams, matches.ToArray());
}
    private async Task<List<TournamentParticipantState>> ReadStatesAsync(string tournamentId)
    {
        using var session = await _fixture.Database.Client.StartSessionAsync();
        return await _administration.GetParticipantStatesAsync(session, tournamentId);
    }

    private Task<TournamentParticipantState> DisqualifyAsync(Tournament tournament, Team team) =>
        _service.DisqualifyTeamAsync(tournament.Id,
            new DisqualifyTeamDto { TeamId = team.Id, Reason = "Repeated rule violation" },
            AdministratorId);

    [Fact]
    public async Task DisqualifyTeam_ShouldPersistParticipantState()
    {
        var (tournament, teams, _) = await SeedAsync();
        var state = await DisqualifyAsync(tournament, teams[0]);
        var saved = await ReadStatesAsync(tournament.Id);
        Assert.Equal(TournamentParticipantStatus.Disqualified, state.Status);
        Assert.Contains(saved, s => s.TeamId == teams[0].Id &&
                                    s.Status == TournamentParticipantStatus.Disqualified);
    }

    [Fact]
    public async Task DisqualifyTeam_ShouldAwardTechnicalWins()
    {
        var (tournament, teams, matches) = await SeedAsync();
        await DisqualifyAsync(tournament, teams[0]);
        var first = await _matches.GetByIdAsync(matches[0].Id);
        var second = await _matches.GetByIdAsync(matches[1].Id);
        var unrelated = await _matches.GetByIdAsync(matches[2].Id);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(unrelated);
        Assert.Equal(MatchStatus.Completed, first.Status);
        Assert.Equal(MatchResultType.Disqualification, first.ResultType);
        Assert.Equal(teams[1].Id, first.WinnerId);
        Assert.Equal(0, first.HomeScore);
        Assert.Equal(1, first.AwayScore);
        Assert.Equal(MatchStatus.Completed, second.Status);
        Assert.Equal(MatchResultType.Disqualification, second.ResultType);
        Assert.Equal(teams[2].Id, second.WinnerId);
        Assert.Equal(1, second.HomeScore);
        Assert.Equal(0, second.AwayScore);
        Assert.NotEqual(MatchStatus.Completed, unrelated.Status);
    }

    [Fact]
    public async Task DisqualifyTeam_ShouldPreserveCompletedMatches()
    {
        var (tournament, teams, matches) = await SeedAsync(completedMatch: true);
        await DisqualifyAsync(tournament, teams[0]);
        var saved = await _matches.GetByIdAsync(matches[0].Id);
        Assert.NotNull(saved);
        Assert.Equal(MatchStatus.Completed, saved.Status);
        Assert.Equal(2, saved.HomeScore);
        Assert.Equal(0, saved.AwayScore);
        Assert.Equal(teams[0].Id, saved.WinnerId);
        Assert.Equal(MatchResultType.Normal, saved.ResultType);
    }

    [Fact]
    public async Task DisqualifyTeam_Twice_ShouldRejectSecondAttempt()
    {
        var (tournament, teams, _) = await SeedAsync();
        await DisqualifyAsync(tournament, teams[0]);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => DisqualifyAsync(tournament, teams[0]));
        var states = await ReadStatesAsync(tournament.Id);
        Assert.Single(states, s => s.TeamId == teams[0].Id);
        var audit = await _administration.GetAuditAsync(tournament.Id);
        Assert.Single(audit, a => a.Action == "TeamDisqualified");
    }
    [Fact]
    public async Task DisqualifyTeam_WhenBatchFails_ShouldRollbackAllChanges()
    {
        var (tournament, teams, matches) = await SeedAsync();
        var tournaments = new TournamentRepository(_fixture.Database);
        var access = new TournamentAccessService(
            tournaments, new TournamentStaffRepository(_fixture.Database));

        var resultService = new Mock<IMatchResultService>();
        resultService
            .Setup(x => x.SetAdministrativeResultsBatchAsync(
                It.IsAny<IClientSessionHandle>(),
                It.IsAny<IReadOnlyCollection<AdministrativeMatchResultRequest>>(),
                It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Simulated batch failure"));

        var service = new TournamentAdministrationService(
            _administration,
            tournaments,
            new TeamRepository(_fixture.Database),
            access,
            _fixture.Database.Client,
            _fixture.Database,
            _matches,
            resultService.Object,
            new DisqualificationPlanner());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DisqualifyTeamAsync(
                tournament.Id,
                new DisqualifyTeamDto
                {
                    TeamId = teams[0].Id,
                    Reason = "Repeated rule violation"
                },
                AdministratorId));

        Assert.Equal("Simulated batch failure", exception.Message);
        resultService.Verify(x => x.SetAdministrativeResultsBatchAsync(
            It.IsAny<IClientSessionHandle>(),
            It.IsAny<IReadOnlyCollection<AdministrativeMatchResultRequest>>(),
            AdministratorId), Times.Once);

        var states = await ReadStatesAsync(tournament.Id);
        Assert.DoesNotContain(states, x => x.TeamId == teams[0].Id);

        var audit = await _administration.GetAuditAsync(tournament.Id);
        Assert.DoesNotContain(audit, x =>
            x.Action == "TeamDisqualified");

        foreach (var original in matches)
        {
            var saved = await _matches.GetByIdAsync(original.Id);
            Assert.NotNull(saved);
            Assert.Equal(original.Status, saved.Status);
            Assert.Equal(original.HomeScore, saved.HomeScore);
            Assert.Equal(original.AwayScore, saved.AwayScore);
            Assert.Equal(original.WinnerId, saved.WinnerId);
        }
    }
    [Fact]
    public async Task DisqualifyTeam_ShouldCreateAuditEntry()
    {
        var (tournament, teams, _) = await SeedAsync();
        var state = await DisqualifyAsync(tournament, teams[0]);
        var audit = await _administration.GetAuditAsync(tournament.Id);
        var entry = Assert.Single(audit, a => a.Action == "TeamDisqualified");
        Assert.Equal(AdministratorId, entry.ActorId);
        Assert.Equal(state.Id, entry.EntityId);
        Assert.Equal("Repeated rule violation", entry.Reason);
        Assert.NotNull(entry.AfterJson);
    }

[Fact]
public async Task DisqualifyTeam_Concurrently_ShouldApplyOnlyOnce()
{
    var (tournament, teams, matches) = await SeedAsync();

    var start = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    async Task<Exception?> TryDisqualifyAsync()
    {
        await start.Task;

        try
        {
            await DisqualifyAsync(tournament, teams[0]);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    var firstTask = Task.Run(TryDisqualifyAsync);
    var secondTask = Task.Run(TryDisqualifyAsync);

    start.SetResult(true);

    var results = await Task.WhenAll(firstTask, secondTask);

    // Рівно одна операція має завершитися успішно.
    Assert.Single(results, ex => ex == null);
    Assert.Single(results, ex => ex != null);

    // Команда дискваліфікована лише один раз.
    var states = await ReadStatesAsync(tournament.Id);

    var state = Assert.Single(
        states,
        s => s.TeamId == teams[0].Id);

    Assert.Equal(
        TournamentParticipantStatus.Disqualified,
        state.Status);
    var audit = await _administration.GetAuditAsync(tournament.Id);

    Assert.Single(
        audit,
        a => a.Action == "TeamDisqualified" &&
             a.EntityId == state.Id);

    var first = await _matches.GetByIdAsync(matches[0].Id);
    var second = await _matches.GetByIdAsync(matches[1].Id);
    var unrelated = await _matches.GetByIdAsync(matches[2].Id);

    Assert.NotNull(first);
    Assert.NotNull(second);
    Assert.NotNull(unrelated);

    Assert.Equal(MatchStatus.Completed, first.Status);
    Assert.Equal(MatchResultType.Disqualification, first.ResultType);
    Assert.Equal(teams[1].Id, first.WinnerId);

    Assert.Equal(MatchStatus.Completed, second.Status);
    Assert.Equal(MatchResultType.Disqualification, second.ResultType);
    Assert.Equal(teams[2].Id, second.WinnerId);

    Assert.NotEqual(MatchStatus.Completed, unrelated.Status);
}

[Fact]
public async Task DisqualifyTeam_WhenAllMatchesCompleted_ShouldFinalizeTournament()
{
    var (tournament, teams, matches) = await SeedAsync();

    var unrelated = await _matches.GetByIdAsync(matches[2].Id);

    Assert.NotNull(unrelated);

    unrelated.Status = MatchStatus.Completed;
    unrelated.HomeScore = 2;
    unrelated.AwayScore = 0;
    unrelated.WinnerId = teams[1].Id;
    unrelated.ResultType = MatchResultType.Normal;

    await _fixture.Database
        .GetCollection<Match>("Matches")
        .ReplaceOneAsync(
            m => m.Id == unrelated.Id,
            unrelated);

    await DisqualifyAsync(tournament, teams[0]);

    var savedMatches = await _matches
        .GetMatchesByTournamentIdAsync(tournament.Id);

    Assert.Equal(3, savedMatches.Count);

    Assert.All(savedMatches, match =>
        Assert.Equal(MatchStatus.Completed, match.Status));

    Assert.Equal(
        2,
        savedMatches.Count(m =>
            m.ResultType == MatchResultType.Disqualification));
    var savedTournament = await _fixture.Database
        .GetCollection<Tournament>("Tournaments")
        .Find(t => t.Id == tournament.Id)
        .FirstOrDefaultAsync();

    Assert.NotNull(savedTournament);

    Assert.Equal(
        TournamentStatus.Completed,
        savedTournament.Status);

    var states = await ReadStatesAsync(tournament.Id);

    Assert.Single(
        states,
        s => s.TeamId == teams[0].Id &&
             s.Status == TournamentParticipantStatus.Disqualified);
}
[Fact]
public async Task GroupStage_Disqualification_ShouldAwardTechnicalWins()
{
    var (tournament, teams, matches) =
        await SeedGroupStageAsync();

    await DisqualifyAsync(tournament, teams[0]);

    var first = await _matches.GetByIdAsync(matches[0].Id);
    var second = await _matches.GetByIdAsync(matches[1].Id);
    var unrelated = await _matches.GetByIdAsync(matches[2].Id);

    Assert.NotNull(first);
    Assert.NotNull(second);
    Assert.NotNull(unrelated);

    Assert.Equal(MatchStatus.Completed, first.Status);
    Assert.Equal(MatchResultType.Disqualification, first.ResultType);
    Assert.Equal(teams[1].Id, first.WinnerId);
    Assert.Equal(0, first.HomeScore);
    Assert.Equal(1, first.AwayScore);

    Assert.Equal(MatchStatus.Completed, second.Status);
    Assert.Equal(MatchResultType.Disqualification, second.ResultType);
    Assert.Equal(teams[2].Id, second.WinnerId);
    Assert.Equal(1, second.HomeScore);
    Assert.Equal(0, second.AwayScore);

    Assert.NotEqual(MatchStatus.Completed, unrelated.Status);

    var states = await ReadStatesAsync(tournament.Id);

    Assert.Contains(states, s =>
        s.TeamId == teams[0].Id &&
        s.Status == TournamentParticipantStatus.Disqualified);
}

[Fact]
public async Task GroupStage_Disqualification_ShouldPreserveCompletedMatches()
{
    var (tournament, teams, matches) =
        await SeedGroupStageAsync(completedMatch: true);

    await DisqualifyAsync(tournament, teams[0]);

    var completed = await _matches.GetByIdAsync(matches[0].Id);
    var technical = await _matches.GetByIdAsync(matches[1].Id);

    Assert.NotNull(completed);
    Assert.NotNull(technical);

    Assert.Equal(MatchStatus.Completed, completed.Status);
    Assert.Equal(2, completed.HomeScore);
    Assert.Equal(0, completed.AwayScore);
    Assert.Equal(teams[0].Id, completed.WinnerId);
    Assert.Equal(MatchResultType.Normal, completed.ResultType);

    Assert.Equal(MatchStatus.Completed, technical.Status);
    Assert.Equal(MatchResultType.Disqualification, technical.ResultType);
    Assert.Equal(teams[2].Id, technical.WinnerId);
}

[Fact]
public async Task GroupStage_Disqualification_AfterPlayoffGeneration_ShouldReject()
{
    var (tournament, teams, matches) =
        await SeedGroupStageAsync(includePlayoff: true);

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        DisqualifyAsync(tournament, teams[0]));

    var states = await ReadStatesAsync(tournament.Id);

    Assert.DoesNotContain(states, s =>
        s.TeamId == teams[0].Id);

    var savedMatches = await _matches
        .GetMatchesByTournamentIdAsync(tournament.Id);

    Assert.Equal(matches.Length, savedMatches.Count);

    Assert.All(savedMatches, match =>
        Assert.NotEqual(MatchStatus.Completed, match.Status));

    var audit = await _administration.GetAuditAsync(tournament.Id);

    Assert.DoesNotContain(audit, a =>
        a.Action == "TeamDisqualified");
}

[Fact]
public async Task GroupStage_Disqualification_ShouldNotFinalizeTournament()
{
    var (tournament, teams, matches) =
        await SeedGroupStageAsync();

    // Завершуємо матч, який не стосується
    // дискваліфікованої команди.
    var unrelated = await _matches.GetByIdAsync(matches[2].Id);

    Assert.NotNull(unrelated);

    unrelated.Status = MatchStatus.Completed;
    unrelated.HomeScore = 2;
    unrelated.AwayScore = 0;
    unrelated.WinnerId = teams[1].Id;
    unrelated.ResultType = MatchResultType.Normal;

    await _fixture.Database
        .GetCollection<Match>("Matches")
        .ReplaceOneAsync(
            m => m.Id == unrelated.Id,
            unrelated);

    await DisqualifyAsync(tournament, teams[0]);

    var savedMatches = await _matches
        .GetMatchesByTournamentIdAsync(tournament.Id);

    Assert.All(savedMatches, match =>
        Assert.Equal(MatchStatus.Completed, match.Status));

    var savedTournament = await _fixture.Database
        .GetCollection<Tournament>("Tournaments")
        .Find(t => t.Id == tournament.Id)
        .FirstOrDefaultAsync();

    Assert.NotNull(savedTournament);

    Assert.Equal(
        TournamentStatus.InProgress,
        savedTournament.Status);
}
}