using Beckend.Repositories.TournamentAdministration;
using Beckend.Enums;
using Beckend.Models;
using Beckend.Models.Olympic;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Olympic;
using Beckend.Repositories.TournamentGroups;
using Beckend.Repositories.Tournaments;
using Beckend.Services.TournamentGenerators;
using Beckend.Services.TournamentStandings;
using MongoDB.Driver;
using Xunit;

namespace Beckend.Tests.Integration;

public class GroupStagePlayoffIntegrationTests
    : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly GroupStagePlayoffService _service;
    private readonly TournamentRepository _tournaments;
    private readonly MatchRepository _matches;
    private readonly TournamentGroupRepository _groups;
    private readonly OlympicBracketRepository _brackets;


public GroupStagePlayoffIntegrationTests(
    MongoDbFixture fixture)
{
    _fixture = fixture;

    _tournaments = new TournamentRepository(fixture.Database);
    _matches = new MatchRepository(fixture.Database);
    _groups = new TournamentGroupRepository(fixture.Database);
    _brackets = new OlympicBracketRepository(fixture.Database);

    var administration = new TournamentAdministrationRepository(
        fixture.Database);

    var standings = new GroupStageStandingsGenerator(
        _groups,
        new RoundRobinStandingsGenerator(),
        administration);

    _service = new GroupStagePlayoffService(
        _tournaments,
        _matches,
        _groups,
        _brackets,
        standings,
        new OlympicGenerator(),
        fixture.Database.Client,
        administration);
}


    private async Task<Tournament> CreateTournamentAsync()
    {
        var tournament = new Tournament
        {
            TournamentName = $"Playoff test {Guid.NewGuid():N}",
            TournamentType = TournamentType.GroupStage,
            Status = TournamentStatus.InProgress,
            TournamentParticipants = Enumerable.Range(1, 16)
                .Select(i => new Team
                {
                    TeamName = $"Team {i}"
                })
                .ToList(),
            Settings = new TournamentSettings
            {
                GroupsCount = 4,
                ParticipantsAdvancing = 8,
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

        var generated = new GroupStageGenerator(
            new RoundRobinGenerator()).Generate(tournament);

        await _fixture.Database
            .GetCollection<TournamentGroup>("TournamentGroups")
            .InsertManyAsync(generated.Groups);

        await _fixture.Database
            .GetCollection<Match>("Matches")
            .InsertManyAsync(generated.Matches);

        return tournament;
    }

    private Task<List<Match>> MatchesAsync(string tournamentId) =>
        _matches.GetMatchesByTournamentIdAsync(tournamentId);

    private async Task CompleteGroupsAsync(string tournamentId)
    {
        var matches = await MatchesAsync(tournamentId);

        foreach (var match in matches)
        {
            var update = Builders<Match>.Update
                .Set(m => m.HomeScore, 2)
                .Set(m => m.AwayScore, 0)
                .Set(m => m.WinnerId, match.HomeTeamId)
                .Set(m => m.Status, MatchStatus.Completed);

            await _fixture.Database
                .GetCollection<Match>("Matches")
                .UpdateOneAsync(m => m.Id == match.Id, update);
        }
    }

    [Fact]
    public async Task CompletedGroups_ShouldCreatePlayoff()
    {
        var tournament = await CreateTournamentAsync();

        await CompleteGroupsAsync(tournament.Id);

        var result = await _service.GenerateAsync(tournament.Id);

        var bracket = await _brackets.GetAsync(
            tournament.Id, "playoff");

        var matches = await MatchesAsync(tournament.Id);
        var playoffMatches = matches
            .Where(m => m.StageId == "playoff")
            .ToList();

        Assert.NotNull(bracket);
        Assert.Equal(8, bracket.ParticipantCount);
        Assert.Equal(8, bracket.BracketSize);
        Assert.Equal(1, bracket.CurrentRound);
        Assert.False(bracket.IsCompleted);

        Assert.Equal(4, playoffMatches.Count);
        Assert.Equal(4, result.MatchesCount);

        Assert.All(playoffMatches, m =>
        {
            Assert.Equal(1, m.RoundNumber);
            Assert.True(m.IsAutomatic);
            Assert.Equal(MatchStatus.Planing, m.Status);
            Assert.InRange(m.BracketPosition, 1, 4);
            Assert.NotEqual(m.HomeTeamId, m.AwayTeamId);
        });

        Assert.Equal(28, matches.Count);
    }

    [Fact]
    public async Task UnfinishedGroups_ShouldRejectGeneration()
    {
        var tournament = await CreateTournamentAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GenerateAsync(tournament.Id));

        Assert.Null(await _brackets.GetAsync(
            tournament.Id, "playoff"));

        Assert.Equal(24,
            (await MatchesAsync(tournament.Id)).Count);
    }

    [Fact]
    public async Task RepeatedGeneration_ShouldNotDuplicatePlayoff()
    {
        var tournament = await CreateTournamentAsync();

        await CompleteGroupsAsync(tournament.Id);

        await _service.GenerateAsync(tournament.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GenerateAsync(tournament.Id));

        var matches = await MatchesAsync(tournament.Id);

        Assert.Equal(4,
            matches.Count(m => m.StageId == "playoff"));

        Assert.Equal(28, matches.Count);
    }

    [Fact]
    public async Task MissingGroupMatch_ShouldRejectGeneration()
    {
        var tournament = await CreateTournamentAsync();

        await CompleteGroupsAsync(tournament.Id);

        var match = (await MatchesAsync(tournament.Id))[0];

        await _fixture.Database
            .GetCollection<Match>("Matches")
            .DeleteOneAsync(m => m.Id == match.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GenerateAsync(tournament.Id));

        Assert.Null(await _brackets.GetAsync(
            tournament.Id, "playoff"));
    }

    [Fact]
    public async Task Playoff_ShouldNotPairTeamsFromSameGroup()
    {
        var tournament = await CreateTournamentAsync();

        await CompleteGroupsAsync(tournament.Id);

        await _service.GenerateAsync(tournament.Id);

        var groups = await _groups
            .GetByTournamentIdAsync(tournament.Id);

        var playoffMatches = (await MatchesAsync(tournament.Id))
            .Where(m => m.StageId == "playoff")
            .ToList();

        foreach (var match in playoffMatches)
        {
            var homeGroup = groups.Single(g =>
                g.TeamIds.Contains(match.HomeTeamId));

            var awayGroup = groups.Single(g =>
                g.TeamIds.Contains(match.AwayTeamId));

            Assert.NotEqual(
                homeGroup.GroupId,
                awayGroup.GroupId);
        }
    }
    private async Task DisqualifyTeamAsync(
    string tournamentId,
    string teamId)
{
    var state = new TournamentParticipantState
    {
        TournamentId = tournamentId,
        TeamId = teamId,
        Status = TournamentParticipantStatus.Disqualified,
        Reason = "Integration test disqualification",
        UpdatedBy = "test-admin",
        UpdatedAt = DateTime.UtcNow
    };

    await _fixture.Database
        .GetCollection<TournamentParticipantState>(
            "TournamentParticipantStates")
        .InsertOneAsync(state);
}

[Fact]
public async Task DisqualifiedTeam_ShouldNotEnterPlayoff()
{
    var tournament = await CreateTournamentAsync();

    await CompleteGroupsAsync(tournament.Id);

    var groups = await _groups
        .GetByTournamentIdAsync(tournament.Id);

    var firstGroup = groups.First();

    // Дискваліфікуємо одну команду.
    var disqualifiedId = firstGroup.TeamIds[0];

    await DisqualifyTeamAsync(
        tournament.Id,
        disqualifiedId);

    await _service.GenerateAsync(tournament.Id);

    var bracket = await _brackets.GetAsync(
        tournament.Id, "playoff");

    Assert.NotNull(bracket);

    Assert.DoesNotContain(
        disqualifiedId,
        bracket.Slots);

    Assert.Equal(8, bracket.ParticipantCount);

    var playoffMatches = (await MatchesAsync(tournament.Id))
        .Where(m => m.StageId == "playoff")
        .ToList();

    Assert.Equal(4, playoffMatches.Count);

    Assert.All(playoffMatches, m =>
    {
        Assert.NotEqual(disqualifiedId, m.HomeTeamId);
        Assert.NotEqual(disqualifiedId, m.AwayTeamId);
    });
}

[Fact]
public async Task InsufficientActiveTeams_ShouldRejectPlayoff()
{
    var tournament = await CreateTournamentAsync();

    await CompleteGroupsAsync(tournament.Id);

    var groups = await _groups
        .GetByTournamentIdAsync(tournament.Id);

    var firstGroup = groups.First();

    // У групі залишиться лише одна активна команда.
    foreach (var teamId in firstGroup.TeamIds.Take(3))
    {
        await DisqualifyTeamAsync(
            tournament.Id,
            teamId);
    }

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        _service.GenerateAsync(tournament.Id));

    Assert.Null(await _brackets.GetAsync(
        tournament.Id, "playoff"));

    var playoffMatches = (await MatchesAsync(tournament.Id))
        .Where(m => m.StageId == "playoff")
        .ToList();

    Assert.Empty(playoffMatches);
}

[Fact]
public async Task DisqualifiedTeam_ShouldKeepGroupStatistics()
{
    var tournament = await CreateTournamentAsync();

    await CompleteGroupsAsync(tournament.Id);

    var groups = await _groups
        .GetByTournamentIdAsync(tournament.Id);

    var firstGroup = groups.First();
    var disqualifiedId = firstGroup.TeamIds[0];

    await DisqualifyTeamAsync(
        tournament.Id,
        disqualifiedId);

    var standings = new GroupStageStandingsGenerator(
        _groups,
        new RoundRobinStandingsGenerator(),
        new TournamentAdministrationRepository(
            _fixture.Database));

    var result = await standings.GenerateAsync(
        tournament,
        await MatchesAsync(tournament.Id));

    var section = result.Sections.Single(s =>
        s.SectionId == firstGroup.GroupId);

    var entry = section.Entries.Single(e =>
        e.ParticipantId == disqualifiedId);

    Assert.True(entry.IsDisqualified);
    Assert.Equal("Disqualified", entry.Status);

    // Статистика завершених матчів зберігається.
    Assert.Equal(3, entry.Played);
    Assert.Equal(3,
        entry.Wins + entry.Draws + entry.Losses);

    // Дискваліфікація не змінює історію результатів.
    Assert.All(
        (await MatchesAsync(tournament.Id))
            .Where(m => m.StageId == "groups"),
        m => Assert.Equal(MatchStatus.Completed, m.Status));
}

}
