using Beckend.Repositories.TournamentAdministration;
using AutoMapper;
using Beckend.DTOs;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Swiss;
using Beckend.Repositories.Tournaments;
using Beckend.Repositories.TournamentGroups;
using Beckend.Services.Matches.Administration;
using Beckend.Services.Matches.Generation;
using Beckend.Services.Matches.Results;
using Beckend.Services.TournamentGenerators;
using MongoDB.Driver;
using Moq;
using Xunit;

using Match = Beckend.Models.Match;

namespace Beckend.Tests.Integration;

public class MatchAdministrationIntegrationTests
    : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly TournamentRepository _tournaments;
    private readonly MatchRepository _matches;
    private readonly MatchGenerationService _generation;
    private readonly MatchAdministrationService _administration;

    private const string AdministratorId =
        "507f1f77bcf86cd799439015";

    public MatchAdministrationIntegrationTests(
        MongoDbFixture fixture)
    {
        _fixture = fixture;

        _tournaments = new TournamentRepository(fixture.Database);
        _matches = new MatchRepository(fixture.Database);

        var byes = new SwissByeRepository(fixture.Database);

        var mapper = new Mock<IMapper>();

        mapper.Setup(m => m.Map<List<MatchDto>>(
                It.IsAny<object>()))
            .Returns(new List<MatchDto>());

        mapper.Setup(m => m.Map<MatchDto>(
                It.IsAny<object>()))
            .Returns(new MatchDto());

        var roundRobin = new RoundRobinGenerator();
        var doubleElimination = new DoubleEliminationGenerator();

        var groupRepository =
            new Mock<ITournamentGroupRepository>();

        _generation = new MatchGenerationService(
            _matches,
            _tournaments,
            byes,
            roundRobin,
            new FixedRoundsSwissGenerator(),
            new ThresholdSwissGenerator(),
            fixture.Database.Client,
            mapper.Object,
            new Cs2SwissGenerator(),
            new GroupStageGenerator(roundRobin),
            groupRepository.Object,
            doubleElimination);

        var tournamentAdministration =
            new TournamentAdministrationRepository(fixture.Database);

        var results = new MatchResultService(
    _matches,
    _tournaments,
    byes,
    new TournamentAdministrationRepository(_fixture.Database),
    _fixture.Database.Client,
    mapper.Object,
    new DoubleEliminationGenerator());

        _administration = new MatchAdministrationService(
            _matches,
            _tournaments,
            results);
    }

    private async Task<Tournament> CreateTournamentAsync(
        int teamCount = 4)
    {
        var tournament = new Tournament
        {
            TournamentName =
                $"Administration integration {Guid.NewGuid():N}",

            TournamentType = TournamentType.DoubleElimination,
            Status = TournamentStatus.Ready,

            CreatedBy = AdministratorId,

            DoubleEliminationLastGeneratedRound = 0,

            TournamentParticipants =
                Enumerable.Range(1, teamCount)
                    .Select(i => new Team
                    {
                        TeamName = $"Team {i}"
                    })
                    .ToList(),

            Settings = new TournamentSettings
            {
                AllowDraws = false
            }
        };

        await _fixture.Database
            .GetCollection<Tournament>("Tournaments")
            .InsertOneAsync(tournament);

        return tournament;
    }

    private async Task<(Tournament Tournament, Match Match)>
        CreateMatchAsync()
    {
        var tournament = await CreateTournamentAsync();

        await _generation.GenerateDoubleEliminationRoundAsync(
            tournament.Id);

        var matches =
            await _matches.GetMatchesByTournamentIdAsync(
                tournament.Id);

        var match = matches
            .OrderBy(m => m.RoundNumber)
            .ThenBy(m => m.BracketPosition)
            .First();

        return (tournament, match);
    }

    private static AdminMatchResultDto CreateDto(
        Match match,
        MatchResultType type = MatchResultType.TechnicalWin,
        bool homeWins = true)
    {
        return new AdminMatchResultDto
        {
            ResultType = type,

            WinnerTeamId = homeWins
                ? match.HomeTeamId
                : match.AwayTeamId,

            Reason = "Administrative decision: opponent absent",

            HomeScore = homeWins ? 3 : 0,
            AwayScore = homeWins ? 0 : 3
        };
    }

    private async Task<Match> GetSavedMatchAsync(string id)
    {
        var match = await _matches.GetByIdAsync(id);
        Assert.NotNull(match);
        return match;
    }

    private Task ApplyTechnicalResultAsync(
        Match match,
        MatchResultType type = MatchResultType.TechnicalWin,
        bool homeWins = true)
    {
        return _administration.SetTechnicalResultAsync(
            match.Id,
            CreateDto(match, type, homeWins),
            AdministratorId);
    }

    // 1. Real MongoDB persistence

    [Fact]
    public async Task TechnicalWin_ShouldPersistAllFields()
    {
        var (_, match) = await CreateMatchAsync();

        var before = DateTime.UtcNow;

        await ApplyTechnicalResultAsync(match);

        var after = DateTime.UtcNow;
        var saved = await GetSavedMatchAsync(match.Id);

        Assert.Equal(MatchStatus.Completed, saved.Status);

        Assert.Equal(3, saved.HomeScore);
        Assert.Equal(0, saved.AwayScore);

        Assert.Equal(match.HomeTeamId, saved.WinnerId);

        Assert.Equal(
            MatchResultType.TechnicalWin,
            saved.ResultType);

        Assert.Equal(
            "Administrative decision: opponent absent",
            saved.ResultReason);

        Assert.Equal(
            AdministratorId,
            saved.ResultUpdatedBy);

        Assert.NotNull(saved.ResultUpdatedAt);

        Assert.InRange(
            saved.ResultUpdatedAt!.Value,
            before,
            after);
    }

    // 2. Forfeit persistence

    [Fact]
    public async Task Forfeit_ShouldPersistResultType()
    {
        var (_, match) = await CreateMatchAsync();

        await ApplyTechnicalResultAsync(
            match,
            MatchResultType.Forfeit);

        var saved = await GetSavedMatchAsync(match.Id);

        Assert.Equal(MatchStatus.Completed, saved.Status);
        Assert.Equal(MatchResultType.Forfeit, saved.ResultType);
        Assert.Equal(match.HomeTeamId, saved.WinnerId);
    }

    // 3. Disqualification persistence

    [Fact]
    public async Task Disqualification_ShouldPersistResultType()
    {
        var (_, match) = await CreateMatchAsync();

        await ApplyTechnicalResultAsync(
            match,
            MatchResultType.Disqualification);

        var saved = await GetSavedMatchAsync(match.Id);

        Assert.Equal(
            MatchResultType.Disqualification,
            saved.ResultType);

        Assert.Equal(MatchStatus.Completed, saved.Status);
        Assert.Equal(match.HomeTeamId, saved.WinnerId);
    }

    // 4. Away team victory

    [Fact]
    public async Task AwayTeamTechnicalWin_ShouldPersistCorrectWinner()
    {
        var (_, match) = await CreateMatchAsync();

        await ApplyTechnicalResultAsync(
            match,
            homeWins: false);

        var saved = await GetSavedMatchAsync(match.Id);

        Assert.Equal(0, saved.HomeScore);
        Assert.Equal(3, saved.AwayScore);

        Assert.Equal(match.AwayTeamId, saved.WinnerId);
    }

    // 5. Prevent result overwrite

    [Fact]
    public async Task RepeatedResult_ShouldPreserveOriginalDecision()
    {
        var (_, match) = await CreateMatchAsync();

        await ApplyTechnicalResultAsync(match);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ApplyTechnicalResultAsync(
                match,
                MatchResultType.Disqualification,
                homeWins: false));

        var saved = await GetSavedMatchAsync(match.Id);

        Assert.Equal(
            MatchResultType.TechnicalWin,
            saved.ResultType);

        Assert.Equal(3, saved.HomeScore);
        Assert.Equal(0, saved.AwayScore);
        Assert.Equal(match.HomeTeamId, saved.WinnerId);
    }

    // 6. Reject invalid result without changing MongoDB

    [Fact]
    public async Task InvalidWinner_ShouldNotModifyMatch()
    {
        var (_, match) = await CreateMatchAsync();

        var dto = CreateDto(match);
        dto.WinnerTeamId =
            "507f1f77bcf86cd799439099";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _administration.SetTechnicalResultAsync(
                match.Id,
                dto,
                AdministratorId));

        var saved = await GetSavedMatchAsync(match.Id);

        Assert.NotEqual(MatchStatus.Completed, saved.Status);
        Assert.Null(saved.HomeScore);
        Assert.Null(saved.AwayScore);
        Assert.Null(saved.WinnerId);
        Assert.Null(saved.ResultUpdatedAt);
    }

    // 7. Technical results must allow next round

    [Fact]
    public async Task TechnicalResults_ShouldAllowNextRoundGeneration()
    {
        var tournament = await CreateTournamentAsync();

        await _generation.GenerateDoubleEliminationRoundAsync(
            tournament.Id);

        var firstRound =
            await _matches.GetMatchesByTournamentIdAsync(
                tournament.Id);

        Assert.NotEmpty(firstRound);

        foreach (var match in firstRound)
        {
            await ApplyTechnicalResultAsync(match);
        }

        await _generation.GenerateDoubleEliminationRoundAsync(
            tournament.Id);

        var savedTournament =
            await _tournaments.GetByIdAsync(tournament.Id);

        var allMatches =
            await _matches.GetMatchesByTournamentIdAsync(
                tournament.Id);

        Assert.NotNull(savedTournament);

        Assert.Equal(
            2,
            savedTournament.DoubleEliminationLastGeneratedRound);

        Assert.Contains(
            allMatches,
            m => m.RoundNumber == 2);

        Assert.All(
            allMatches.Where(m => m.RoundNumber == 1),
            m =>
            {
                Assert.Equal(
                    MatchStatus.Completed,
                    m.Status);

                Assert.Equal(
                    MatchResultType.TechnicalWin,
                    m.ResultType);
            });
    }

    // 8. Mixed administrative and normal results

    [Fact]
    public async Task MixedResults_ShouldAllowNextRound()
    {
        var tournament = await CreateTournamentAsync();

        await _generation.GenerateDoubleEliminationRoundAsync(
            tournament.Id);

        var matches =
            await _matches.GetMatchesByTournamentIdAsync(
                tournament.Id);

        Assert.True(matches.Count >= 2);

        var first = matches[0];
        var second = matches[1];

        await ApplyTechnicalResultAsync(
            first,
            MatchResultType.Forfeit);

        var byes = new SwissByeRepository(_fixture.Database);

        var mapper = new Mock<IMapper>();

        mapper.Setup(m => m.Map<MatchDto>(
                It.IsAny<object>()))
            .Returns(new MatchDto());

        var results = new MatchResultService(
    _matches,
    _tournaments,
    byes,
    new TournamentAdministrationRepository(_fixture.Database),
    _fixture.Database.Client,
    mapper.Object,
    new DoubleEliminationGenerator());

        await results.UpdateResultAsync(
            second.Id,
            new UpdateMatchResultDto
            {
                HomeScore = 2,
                AwayScore = 0
            });

        await _generation.GenerateDoubleEliminationRoundAsync(
            tournament.Id);

        var savedFirst = await GetSavedMatchAsync(first.Id);
        var savedSecond = await GetSavedMatchAsync(second.Id);

        Assert.Equal(
            MatchResultType.Forfeit,
            savedFirst.ResultType);

        Assert.Equal(
            MatchResultType.Normal,
            savedSecond.ResultType);

        Assert.Equal(
            MatchStatus.Completed,
            savedFirst.Status);

        Assert.Equal(
            MatchStatus.Completed,
            savedSecond.Status);
    }

    // 9. Full DoubleElimination with technical results

    [Fact]
    public async Task FullTournament_WithTechnicalResults_ShouldComplete()
    {
        var tournament = await CreateTournamentAsync();

        var teamIds = tournament.TournamentParticipants
            .Select(t => t.Id)
            .ToHashSet();

        await _generation.GenerateDoubleEliminationRoundAsync(
            tournament.Id);

        for (var step = 0; step < 35; step++)
        {
            var state =
                await _tournaments.GetByIdAsync(tournament.Id);

            Assert.NotNull(state);

            if (state.Status == TournamentStatus.Completed)
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(state.ChampionId));

                Assert.Contains(state.ChampionId!, teamIds);

                var all =
                    await _matches.GetMatchesByTournamentIdAsync(
                        tournament.Id);

                Assert.All(all, m =>
                {
                    Assert.Equal(
                        MatchStatus.Completed,
                        m.Status);

                    Assert.Equal(
                        MatchResultType.TechnicalWin,
                        m.ResultType);

                    Assert.NotNull(m.ResultUpdatedAt);
                });

                return;
            }

            var pending =
                (await _matches.GetMatchesByTournamentIdAsync(
                    tournament.Id))
                .Where(m => m.Status != MatchStatus.Completed)
                .OrderBy(m => m.RoundNumber)
                .ThenBy(m => m.BracketPosition)
                .ToList();

            Assert.NotEmpty(pending);

            foreach (var match in pending)
            {
                await ApplyTechnicalResultAsync(match);
            }

            state = await _tournaments.GetByIdAsync(tournament.Id);
            Assert.NotNull(state);

            if (state.Status == TournamentStatus.InProgress)
            {
                await _generation
                    .GenerateDoubleEliminationRoundAsync(
                        tournament.Id);
            }
        }

        Assert.Fail(
            "Tournament did not finish within 35 steps.");
    }
}
