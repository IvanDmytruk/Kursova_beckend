using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Swiss;
using Beckend.Repositories.Tournaments;
using Beckend.Services.Matches.Generation;
using Beckend.Services.Matches.Results;
using Beckend.Services.TournamentGenerators;
using Moq;
using Beckend.Repositories.TournamentGroups;
using MongoDB.Driver;
using Xunit;

using Match = Beckend.Models.Match;

namespace Beckend.Tests.Integration;

public class SwissTournamentServiceIntegrationTests
    : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly TournamentRepository _tournaments;
    private readonly MatchRepository _matches;
    private readonly SwissByeRepository _byes;
    private readonly MatchGenerationService _generationService;
    private readonly MatchResultService _resultService;

    public SwissTournamentServiceIntegrationTests(
        MongoDbFixture fixture)
    {
        _fixture = fixture;

        _tournaments = new TournamentRepository(
            fixture.Database);

        _matches = new MatchRepository(
            fixture.Database);

        _byes = new SwissByeRepository(
            fixture.Database);

        var mapper = new Mock<IMapper>();

        mapper.Setup(m => m.Map<List<MatchDto>>(It.IsAny<object>()))
            .Returns(new List<MatchDto>());

        mapper.Setup(m => m.Map<MatchDto>(It.IsAny<object>()))
            .Returns(new MatchDto());

        var groupRepository = new Mock<ITournamentGroupRepository>();

        var roundRobinGenerator = new RoundRobinGenerator();

        var groupStageGenerator =
            new GroupStageGenerator(roundRobinGenerator);

        _generationService = new MatchGenerationService(
            _matches,
            _tournaments,
            _byes,
            roundRobinGenerator,
            new FixedRoundsSwissGenerator(),
            new ThresholdSwissGenerator(),
            fixture.Database.Client,
            mapper.Object,
            new Cs2SwissGenerator(),
            groupStageGenerator,
            groupRepository.Object,
            new DoubleEliminationGenerator());

        _resultService = new MatchResultService(
            _matches,
            _tournaments,
            _byes,
            fixture.Database.Client,
            mapper.Object,
            new DoubleEliminationGenerator());
    }

    private async Task<Tournament> CreateTournamentAsync(
        SwissMode mode,
        int teamCount = 4,
        int rounds = 2,
        int wins = 1,
        int losses = 1)
    {
        var tournament = new Tournament
        {
            TournamentName =
                $"Swiss integration {Guid.NewGuid():N}",

            TournamentType = TournamentType.SwissSystem,
            Status = TournamentStatus.Ready,
            SwissLastGeneratedRound = 0,

            TournamentParticipants =
                Enumerable.Range(1, teamCount)
                    .Select(i => new Team
                    {
                        TeamName = $"Test team {i}"
                    })
                    .ToList(),

            Settings = new TournamentSettings
            {
                SwissMode = mode,
                RoundsCount = rounds,
                WinsToAdvance = wins,
                LossesToEliminate = losses,
                AllowDraws = false,
                AllowRematches = true,
                ByeCountsAsWin = true,
                PointsForWin = 3,
                PointsForLoss = 0,
                PointsForDraw = 1,
                PointsForBye = 3
            }
        };

        await _fixture.Database
            .GetCollection<Tournament>("Tournaments")
            .InsertOneAsync(tournament);

        return tournament;
    }

    private Task<List<Match>> SavedMatchesAsync(
        string tournamentId)
    {
        return _matches.GetMatchesByTournamentIdAsync(
            tournamentId);
    }

    private async Task<List<SwissBye>> SavedByesAsync(
        string tournamentId)
    {
        using var session =
            await _fixture.Database.Client.StartSessionAsync();

        return await _byes.GetByTournamentAsync(
            session,
            tournamentId);
    }

    private async Task CompleteAsync(
        Match match,
        int home = 2,
        int away = 0)
    {
        await _resultService.UpdateResultAsync(
            match.Id,
            new UpdateMatchResultDto
            {
                HomeScore = home,
                AwayScore = away
            });
    }

    [Fact]
    public async Task FixedRounds_FirstRound_ShouldPersistMatchesAndStartTournament()
    {
        var tournament = await CreateTournamentAsync(
            SwissMode.FixedRounds);

        await _generationService.GenerateSwissRoundAsync(
            tournament.Id);

        var saved = await _tournaments.GetByIdAsync(
            tournament.Id);

        var matches = await SavedMatchesAsync(
            tournament.Id);

        Assert.NotNull(saved);

        Assert.Equal(
            TournamentStatus.InProgress,
            saved.Status);

        Assert.Equal(1, saved.SwissLastGeneratedRound);
        Assert.Equal(2, matches.Count);

        Assert.All(matches, m =>
        {
            Assert.Equal(1, m.RoundNumber);
            Assert.Equal(tournament.Id, m.TournamentId);
            Assert.True(m.IsAutomatic);
        });
    }

    [Fact]
    public async Task FixedRounds_SecondGeneration_ShouldNotCreateDuplicates()
    {
        var tournament = await CreateTournamentAsync(
            SwissMode.FixedRounds);

        await _generationService.GenerateSwissRoundAsync(
            tournament.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _generationService.GenerateSwissRoundAsync(
                tournament.Id));

        Assert.Equal(
            2,
            (await SavedMatchesAsync(tournament.Id)).Count);

        var saved = await _tournaments.GetByIdAsync(
            tournament.Id);

        Assert.NotNull(saved);
        Assert.Equal(1, saved.SwissLastGeneratedRound);
    }

    [Fact]
    public async Task FixedRounds_NextRoundBeforeResults_ShouldFailWithoutChanges()
    {
        var tournament = await CreateTournamentAsync(
            SwissMode.FixedRounds);

        await _generationService.GenerateSwissRoundAsync(
            tournament.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _generationService.GenerateSwissRoundAsync(
                tournament.Id));

        Assert.Equal(
            2,
            (await SavedMatchesAsync(tournament.Id)).Count);
    }

    [Fact]
    public async Task FixedRounds_LastCompletedRound_ShouldCompleteTournament()
    {
        var tournament = await CreateTournamentAsync(
            SwissMode.FixedRounds,
            rounds: 2);

        await _generationService.GenerateSwissRoundAsync(
            tournament.Id);

        foreach (var match in await SavedMatchesAsync(tournament.Id))
        {
            await CompleteAsync(match);
        }

        var afterFirstRound =
            await _tournaments.GetByIdAsync(tournament.Id);

        Assert.NotNull(afterFirstRound);

        Assert.Equal(
            TournamentStatus.InProgress,
            afterFirstRound.Status);

        await _generationService.GenerateSwissRoundAsync(
            tournament.Id);

        var allMatches = await SavedMatchesAsync(
            tournament.Id);

        Assert.Equal(4, allMatches.Count);

        Assert.Equal(
            2,
            allMatches.Count(m => m.RoundNumber == 2));

        foreach (var match in allMatches.Where(
                     m => m.RoundNumber == 2))
        {
            await CompleteAsync(match);
        }

        var saved = await _tournaments.GetByIdAsync(
            tournament.Id);

        Assert.NotNull(saved);

        Assert.Equal(
            TournamentStatus.Completed,
            saved.Status);

        Assert.Equal(2, saved.SwissLastGeneratedRound);
    }

    [Fact]
    public async Task Threshold_FirstRound_ShouldPersistMatches()
    {
        var tournament = await CreateTournamentAsync(
            SwissMode.WinLossThreshold);

        await _generationService.GenerateSwissRoundAsync(
            tournament.Id);

        var saved = await _tournaments.GetByIdAsync(
            tournament.Id);

        Assert.NotNull(saved);

        Assert.Equal(
            TournamentStatus.InProgress,
            saved.Status);

        Assert.Equal(1, saved.SwissLastGeneratedRound);

        Assert.Equal(
            2,
            (await SavedMatchesAsync(tournament.Id)).Count);
    }

    [Fact]
    public async Task Threshold_OneWinOneLoss_ShouldCompleteAfterFirstRound()
    {
        var tournament = await CreateTournamentAsync(
            SwissMode.WinLossThreshold,
            wins: 1,
            losses: 1);

        await _generationService.GenerateSwissRoundAsync(
            tournament.Id);

        foreach (var match in await SavedMatchesAsync(tournament.Id))
        {
            await CompleteAsync(match);
        }

        var saved = await _tournaments.GetByIdAsync(
            tournament.Id);

        Assert.NotNull(saved);

        Assert.Equal(
            TournamentStatus.Completed,
            saved.Status);
    }

    [Fact]
    public async Task FixedRounds_OddTeams_ShouldPersistBye()
    {
        var tournament = await CreateTournamentAsync(
            SwissMode.FixedRounds,
            teamCount: 5);

        await _generationService.GenerateSwissRoundAsync(
            tournament.Id);

        var matches = await SavedMatchesAsync(
            tournament.Id);

        var byes = await SavedByesAsync(
            tournament.Id);

        Assert.Equal(2, matches.Count);
        Assert.Single(byes);

        Assert.Equal(1, byes[0].RoundNumber);
        Assert.False(byes[0].CountsAsWin);

        Assert.DoesNotContain(
            matches,
            m => m.HomeTeamId == byes[0].TeamId ||
                 m.AwayTeamId == byes[0].TeamId);
    }

    [Fact]
    public async Task Threshold_OddTeams_ShouldCountByeAsWinAndComplete()
    {
        var tournament = await CreateTournamentAsync(
            SwissMode.WinLossThreshold,
            teamCount: 5,
            wins: 1,
            losses: 1);

        await _generationService.GenerateSwissRoundAsync(
            tournament.Id);

        var byes = await SavedByesAsync(
            tournament.Id);

        Assert.Single(byes);
        Assert.True(byes[0].CountsAsWin);

        Assert.Equal(
            2,
            (await SavedMatchesAsync(tournament.Id)).Count);

        foreach (var match in await SavedMatchesAsync(tournament.Id))
        {
            await CompleteAsync(match);
        }

        var saved = await _tournaments.GetByIdAsync(
            tournament.Id);

        Assert.NotNull(saved);

        Assert.Equal(
            TournamentStatus.Completed,
            saved.Status);
    }

    [Fact]
    public async Task Threshold_Draw_ShouldFailWithoutChangingMatch()
    {
        var tournament = await CreateTournamentAsync(
            SwissMode.WinLossThreshold);

        await _generationService.GenerateSwissRoundAsync(
            tournament.Id);

        var match = (await SavedMatchesAsync(
            tournament.Id))[0];

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CompleteAsync(match, 1, 1));

        var saved = await _matches.GetByIdAsync(
            match.Id);

        Assert.NotNull(saved);
        Assert.NotEqual(MatchStatus.Completed, saved.Status);
        Assert.Null(saved.HomeScore);
        Assert.Null(saved.AwayScore);
    }

    [Fact]
    public async Task UpdatingResultTwice_ShouldFailAndPreserveFirstScore()
    {
        var tournament = await CreateTournamentAsync(
            SwissMode.FixedRounds);

        await _generationService.GenerateSwissRoundAsync(
            tournament.Id);

        var match = (await SavedMatchesAsync(
            tournament.Id))[0];

        await CompleteAsync(match, 2, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CompleteAsync(match, 0, 2));

        var saved = await _matches.GetByIdAsync(
            match.Id);

        Assert.NotNull(saved);

        Assert.Equal(MatchStatus.Completed, saved.Status);
        Assert.Equal(2, saved.HomeScore);
        Assert.Equal(0, saved.AwayScore);
    }
}
