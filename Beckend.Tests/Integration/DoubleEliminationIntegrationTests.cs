using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Swiss;
using Beckend.Repositories.Tournaments;
using Beckend.Repositories.TournamentGroups;
using Beckend.Services.Matches.Generation;
using Beckend.Services.Matches.Results;
using Beckend.Services.TournamentGenerators;
using MongoDB.Driver;
using Moq;
using Xunit;

using Match = Beckend.Models.Match;

namespace Beckend.Tests.Integration;

public class DoubleEliminationIntegrationTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly TournamentRepository _tournaments;
    private readonly MatchRepository _matches;
    private readonly SwissByeRepository _byes;
    private readonly MatchGenerationService _generation;
    private readonly MatchResultService _results;

    public DoubleEliminationIntegrationTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _tournaments = new TournamentRepository(fixture.Database);
        _matches = new MatchRepository(fixture.Database);
        _byes = new SwissByeRepository(fixture.Database);

        // DTO mapping is not under test; repositories and MongoDB are real.
        var mapper = new Mock<IMapper>();
        mapper.Setup(m => m.Map<List<MatchDto>>(It.IsAny<object>()))
            .Returns(new List<MatchDto>());
        mapper.Setup(m => m.Map<MatchDto>(It.IsAny<object>()))
            .Returns(new MatchDto());

        var roundRobin = new RoundRobinGenerator();
        var doubleElimination = new DoubleEliminationGenerator();
        var groupRepository = new Mock<ITournamentGroupRepository>();

        _generation = new MatchGenerationService(
            _matches, _tournaments, _byes,
            roundRobin,
            new FixedRoundsSwissGenerator(),
            new ThresholdSwissGenerator(),
            fixture.Database.Client,
            mapper.Object,
            new Cs2SwissGenerator(),
            new GroupStageGenerator(roundRobin),
            groupRepository.Object,
            doubleElimination);

        _results = new MatchResultService(
            _matches, _tournaments, _byes,
            fixture.Database.Client, mapper.Object,
            doubleElimination);
    }

    private async Task<Tournament> CreateTournamentAsync(int teamCount = 4)
    {
        var tournament = new Tournament
        {
            TournamentName = $"Double elimination integration {Guid.NewGuid():N}",
            TournamentType = TournamentType.DoubleElimination,
            Status = TournamentStatus.Ready,
            DoubleEliminationLastGeneratedRound = 0,
            TournamentParticipants = Enumerable.Range(1, teamCount)
                .Select(i => new Team { TeamName = $"Team {i}" })
                .ToList(),
            Settings = new TournamentSettings { AllowDraws = false }
        };

        await _fixture.Database.GetCollection<Tournament>("Tournaments")
            .InsertOneAsync(tournament);
        return tournament;
    }
    private Task<List<Match>> MatchesAsync(string tournamentId) =>
        _matches.GetMatchesByTournamentIdAsync(tournamentId);

    private async Task<List<SwissBye>> ByesAsync(string tournamentId)
    {
        using var session = await _fixture.Database.Client.StartSessionAsync();
        return await _byes.GetByTournamentAsync(session, tournamentId);
    }

    private async Task CompleteAsync(Match match, bool homeWins = true)
    {
        await _results.UpdateResultAsync(match.Id, new UpdateMatchResultDto
        {
            HomeScore = homeWins ? 2 : 0,
            AwayScore = homeWins ? 0 : 2
        });
    }

    [Fact]
    public async Task FirstRound_ShouldPersistMatchesAndStartTournament()
    {
        var tournament = await CreateTournamentAsync();
        await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);

        var saved = await _tournaments.GetByIdAsync(tournament.Id);
        var matches = await MatchesAsync(tournament.Id);

        Assert.NotNull(saved);
        Assert.Equal(TournamentStatus.InProgress, saved.Status);
        Assert.Equal(1, saved.DoubleEliminationLastGeneratedRound);
        Assert.NotEmpty(matches);
        Assert.All(matches, match =>
        {
            Assert.Equal(tournament.Id, match.TournamentId);
            Assert.Equal(1, match.RoundNumber);
            Assert.True(match.IsAutomatic);
            Assert.Contains(match.StageId, new[] { "double-main", "grand-final" });
            Assert.NotEqual(match.HomeTeamId, match.AwayTeamId);
        });
    }

    [Fact]
    public async Task NextRoundBeforeResults_ShouldFailWithoutDuplicates()
    {
        var tournament = await CreateTournamentAsync();
        await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);
        var before = await MatchesAsync(tournament.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _generation.GenerateDoubleEliminationRoundAsync(tournament.Id));

        var after = await MatchesAsync(tournament.Id);
        var saved = await _tournaments.GetByIdAsync(tournament.Id);
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(1, saved!.DoubleEliminationLastGeneratedRound);
    }

    [Fact]
    public async Task OddTeams_ShouldPersistByeWithoutWinOrPoints()
    {
        var tournament = await CreateTournamentAsync(teamCount: 5);
        await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);

        var byes = await ByesAsync(tournament.Id);
        var matches = await MatchesAsync(tournament.Id);
        var bye = Assert.Single(byes);

        Assert.Equal(1, bye.RoundNumber);
        Assert.Equal(tournament.Id, bye.TournamentId);
        Assert.False(bye.CountsAsWin);
        Assert.Equal(0m, bye.PointsAwarded);
        Assert.DoesNotContain(matches, m =>
            m.HomeTeamId == bye.TeamId || m.AwayTeamId == bye.TeamId);
    }
    [Fact]
    public async Task Draw_ShouldFailAndPreserveUnfinishedMatch()
    {
        var tournament = await CreateTournamentAsync();
        await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);
        var match = (await MatchesAsync(tournament.Id))[0];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _results.UpdateResultAsync(match.Id, new UpdateMatchResultDto
            {
                HomeScore = 1,
                AwayScore = 1
            }));

        var saved = await _matches.GetByIdAsync(match.Id);
        Assert.NotNull(saved);
        Assert.NotEqual(MatchStatus.Completed, saved.Status);
        Assert.Null(saved.HomeScore);
        Assert.Null(saved.AwayScore);
    }

    [Fact]
    public async Task UpdatingResultTwice_ShouldPreserveFirstResult()
    {
        var tournament = await CreateTournamentAsync();
        await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);
        var match = (await MatchesAsync(tournament.Id))[0];

        await CompleteAsync(match);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _results.UpdateResultAsync(match.Id, new UpdateMatchResultDto
            {
                HomeScore = 0,
                AwayScore = 2
            }));

        var saved = await _matches.GetByIdAsync(match.Id);
        Assert.NotNull(saved);
        Assert.Equal(MatchStatus.Completed, saved.Status);
        Assert.Equal(2, saved.HomeScore);
        Assert.Equal(0, saved.AwayScore);
        Assert.Equal(match.HomeTeamId, saved.WinnerId);
    }

    [Fact]
    public async Task CompletedRound_ShouldAllowNextRound()
    {
        var tournament = await CreateTournamentAsync();
        await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);
        var firstRound = await MatchesAsync(tournament.Id);
        foreach (var match in firstRound)
            await CompleteAsync(match);

        await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);
        var allMatches = await MatchesAsync(tournament.Id);
        var saved = await _tournaments.GetByIdAsync(tournament.Id);

        Assert.NotNull(saved);
        Assert.Equal(TournamentStatus.InProgress, saved.Status);
        Assert.Equal(2, saved.DoubleEliminationLastGeneratedRound);
        Assert.Contains(allMatches, m => m.RoundNumber == 2);
        Assert.Equal(firstRound.Count, allMatches.Count(m => m.RoundNumber == 1));
    }
    [Fact]
    public async Task FullTournament_ShouldCompleteAndPersistChampion()
    {
        var tournament = await CreateTournamentAsync();
        var teamIds = tournament.TournamentParticipants.Select(t => t.Id).ToHashSet();
        await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);

        // The loop guard makes a stalled tournament fail instead of hanging.
        for (var round = 1; round <= 30; round++)
        {
            var current = await _tournaments.GetByIdAsync(tournament.Id);
            Assert.NotNull(current);
            if (current.Status == TournamentStatus.Completed)
            {
                Assert.False(string.IsNullOrWhiteSpace(current.ChampionId));
                Assert.Contains(current.ChampionId!, teamIds);
                Assert.All(await MatchesAsync(tournament.Id), m =>
                {
                    Assert.Equal(MatchStatus.Completed, m.Status);
                    Assert.NotNull(m.HomeScore);
                    Assert.NotNull(m.AwayScore);
                });
                return;
            }

            var unfinished = (await MatchesAsync(tournament.Id))
                .Where(m => m.Status != MatchStatus.Completed)
                .OrderBy(m => m.RoundNumber)
                .ThenBy(m => m.BracketPosition)
                .ToList();
            Assert.NotEmpty(unfinished);

            foreach (var match in unfinished)
                await CompleteAsync(match);

            current = await _tournaments.GetByIdAsync(tournament.Id);
            Assert.NotNull(current);
            if (current.Status == TournamentStatus.Completed)
                continue;

            await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);
        }

        Assert.Fail("DoubleElimination did not complete within 30 rounds.");
    }

[Fact]
public async Task GrandFinal_UndefeatedTeamLoses_ShouldGenerateResetAndComplete()
{
    var tournament = await CreateTournamentAsync(teamCount: 4);
    var firstGrandFinalId = (string?)null;
    string? expectedChampion = null;

    await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);

    for (var step = 0; step < 35; step++)
    {
        var state = await _tournaments.GetByIdAsync(tournament.Id);
        Assert.NotNull(state);

        if (state.Status == TournamentStatus.Completed)
        {
            Assert.NotNull(firstGrandFinalId);
            Assert.Equal(expectedChampion, state.ChampionId);

            var finals = (await MatchesAsync(tournament.Id))
                .Where(m => m.StageId == "grand-final")
                .OrderBy(m => m.RoundNumber)
                .ToList();

            Assert.True(finals.Count >= 2,
                "A loss by the undefeated finalist must produce a grand-final reset.");

            Assert.All(finals,
                m => Assert.Equal(MatchStatus.Completed, m.Status));

            return;
        }

        var matches = await MatchesAsync(tournament.Id);

        var pending = matches
            .Where(m => m.Status != MatchStatus.Completed)
            .OrderBy(m => m.RoundNumber)
            .ThenBy(m => m.BracketPosition)
            .ToList();

        Assert.NotEmpty(pending);

        foreach (var match in pending)
        {
            if (match.StageId != "grand-final")
            {
                await CompleteAsync(match);
                continue;
            }

            var priorLosses = matches
                .Where(m => m.Status == MatchStatus.Completed &&
                            m.HomeScore.HasValue &&
                            m.AwayScore.HasValue)
                .GroupBy(m =>
                    m.HomeScore > m.AwayScore
                        ? m.AwayTeamId
                        : m.HomeTeamId)
                .ToDictionary(g => g.Key, g => g.Count());

            int homeLosses = priorLosses.GetValueOrDefault(match.HomeTeamId);
            int awayLosses = priorLosses.GetValueOrDefault(match.AwayTeamId);

            if (firstGrandFinalId == null)
            {
                Assert.True(
                    (homeLosses == 0 && awayLosses == 1) ||
                    (homeLosses == 1 && awayLosses == 0),
                    $"Expected undefeated vs one-loss finalist, got {homeLosses} and {awayLosses} losses.");

                firstGrandFinalId = match.Id;

                // Перемагає команда з однією поразкою.
                bool homeWins = homeLosses == 1;

                await CompleteAsync(match, homeWins);

                expectedChampion = homeWins
                    ? match.HomeTeamId
                    : match.AwayTeamId;
            }
            else
            {
                Assert.NotEqual(firstGrandFinalId, match.Id);

                // Та сама команда виграє повторний фінал.
                await CompleteAsync(
                    match,
                    match.HomeTeamId == expectedChampion);
            }
        }

        state = await _tournaments.GetByIdAsync(tournament.Id);
        Assert.NotNull(state);

        if (state.Status == TournamentStatus.InProgress)
        {
            await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);
        }
    }

    Assert.Fail("Grand final reset did not finish within 35 rounds.");
}

[Fact]
public async Task EightTeams_TwoLossesEliminateTeamAndChampionIsPersisted()
{
    var tournament = await CreateTournamentAsync(teamCount: 8);

    var teamIds = tournament.TournamentParticipants
        .Select(t => t.Id)
        .ToHashSet();

    await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);

    for (var step = 0; step < 45; step++)
    {
        var state = await _tournaments.GetByIdAsync(tournament.Id);
        Assert.NotNull(state);

        if (state.Status == TournamentStatus.Completed)
        {
            Assert.Contains(state.ChampionId!, teamIds);

            var all = await MatchesAsync(tournament.Id);

            Assert.All(all,
                m => Assert.Equal(MatchStatus.Completed, m.Status));

            var losses = all
                .GroupBy(m =>
                    m.HomeScore > m.AwayScore
                        ? m.AwayTeamId
                        : m.HomeTeamId)
                .ToDictionary(g => g.Key, g => g.Count());

            // Усі 7 суперників чемпіона мають дві поразки.
            Assert.Equal(
                7,
                teamIds.Count(id =>
                    losses.GetValueOrDefault(id) >= 2));

            // Чемпіон може мати максимум одну поразку.
            Assert.True(
                losses.GetValueOrDefault(state.ChampionId!) <= 1);

            return;
        }

        var allMatches = await MatchesAsync(tournament.Id);

        var finished = allMatches
            .Where(m => m.Status == MatchStatus.Completed)
            .ToList();

        var lossesBeforeRound = finished
            .GroupBy(m =>
                m.HomeScore > m.AwayScore
                    ? m.AwayTeamId
                    : m.HomeTeamId)
            .ToDictionary(g => g.Key, g => g.Count());

        var pending = allMatches
            .Where(m => m.Status != MatchStatus.Completed)
            .OrderBy(m => m.RoundNumber)
            .ThenBy(m => m.BracketPosition)
            .ToList();

        Assert.NotEmpty(pending);

        // Команди з двома поразками не можуть грати далі.
        Assert.All(pending, m =>
        {
            Assert.True(
                lossesBeforeRound.GetValueOrDefault(m.HomeTeamId) < 2);

            Assert.True(
                lossesBeforeRound.GetValueOrDefault(m.AwayTeamId) < 2);

            Assert.Contains(m.HomeTeamId, teamIds);
            Assert.Contains(m.AwayTeamId, teamIds);
        });

        foreach (var match in pending)
        {
            await CompleteAsync(match);
        }

        state = await _tournaments.GetByIdAsync(tournament.Id);
        Assert.NotNull(state);

        if (state.Status == TournamentStatus.InProgress)
        {
            await _generation.GenerateDoubleEliminationRoundAsync(tournament.Id);
        }
    }

    Assert.Fail("Eight-team DoubleElimination did not finish within 45 rounds.");
}

}