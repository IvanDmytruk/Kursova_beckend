using Beckend.Enums;
using Beckend.Models;
using Beckend.Models.Olympic;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Olympic;
using Beckend.Repositories.TournamentAdministration;
using Beckend.Repositories.TournamentGroups;
using Beckend.Repositories.Tournaments;
using Beckend.Services.Olympic;
using Beckend.Services.TournamentGenerators;
using Beckend.Services.TournamentStandings;
using MongoDB.Driver;
using Xunit;

namespace Beckend.Tests.Integration;

public class GroupStagePlayoffLifecycleIntegrationTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly GroupStagePlayoffService _playoff;
    private readonly OlympicBracketService _bracketService;
    private readonly TournamentRepository _tournaments;
    private readonly MatchRepository _matches;
    private readonly OlympicBracketRepository _brackets;

    public GroupStagePlayoffLifecycleIntegrationTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        _tournaments = new TournamentRepository(fixture.Database);
        _matches = new MatchRepository(fixture.Database);
        var groups = new TournamentGroupRepository(fixture.Database);
        _brackets = new OlympicBracketRepository(fixture.Database);
        var administration = new TournamentAdministrationRepository(fixture.Database);
        var olympicGenerator = new OlympicGenerator();

        _playoff = new GroupStagePlayoffService(
            _tournaments, _matches, groups, _brackets,
            new GroupStageStandingsGenerator(
                groups, new RoundRobinStandingsGenerator(), administration),
            olympicGenerator, fixture.Database.Client, administration);

        _bracketService = new OlympicBracketService(
            _tournaments, _matches, _brackets,
            olympicGenerator, fixture.Database.Client);
    }

    private async Task<Tournament> CreateTournamentAsync()
    {
        var tournament = new Tournament
        {
            TournamentName = $"Lifecycle test {Guid.NewGuid():N}",
            TournamentType = TournamentType.GroupStage,
            Status = TournamentStatus.InProgress,
            TournamentParticipants = Enumerable.Range(1, 16)
                .Select(i => new Team { TeamName = $"Lifecycle Team {i}" })
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

        await _fixture.Database.GetCollection<Tournament>("Tournaments")
            .InsertOneAsync(tournament);

        var generated = new GroupStageGenerator(new RoundRobinGenerator())
            .Generate(tournament);

        await _fixture.Database.GetCollection<TournamentGroup>("TournamentGroups")
            .InsertManyAsync(generated.Groups);
        await _fixture.Database.GetCollection<Match>("Matches")
            .InsertManyAsync(generated.Matches);

        return tournament;
    }
    private Task<List<Match>> GetMatchesAsync(string tournamentId) =>
        _matches.GetMatchesByTournamentIdAsync(tournamentId);

    private async Task CompleteMatchesAsync(IEnumerable<Match> matches)
    {
        var collection = _fixture.Database.GetCollection<Match>("Matches");
        foreach (var match in matches)
        {
            var update = Builders<Match>.Update
                .Set(m => m.HomeScore, 2)
                .Set(m => m.AwayScore, 0)
                .Set(m => m.WinnerId, match.HomeTeamId)
                .Set(m => m.Status, MatchStatus.Completed);

            var result = await collection.UpdateOneAsync(
                m => m.Id == match.Id, update);
            Assert.Equal(1, result.MatchedCount);
        }
    }

    private async Task<Tournament> CreatePlayoffAsync()
    {
        var tournament = await CreateTournamentAsync();
        var groupMatches = (await GetMatchesAsync(tournament.Id))
            .Where(m => m.StageId == "groups").ToList();
        Assert.Equal(24, groupMatches.Count);
        await CompleteMatchesAsync(groupMatches);
        await _playoff.GenerateAsync(tournament.Id);
        return tournament;
    }

    private async Task<List<Match>> GetRoundAsync(string tournamentId, int round) =>
        (await GetMatchesAsync(tournamentId))
            .Where(m => m.StageId == "playoff" && m.RoundNumber == round)
            .OrderBy(m => m.BracketPosition)
            .ToList();

    [Fact]
    public async Task FullLifecycle_ShouldCompleteTournamentAndSaveChampion()
    {
        var tournament = await CreatePlayoffAsync();
        var quarterfinals = await GetRoundAsync(tournament.Id, 1);
        Assert.Equal(4, quarterfinals.Count);

        await CompleteMatchesAsync(quarterfinals);
        await _bracketService.GenerateNextRoundAsync(tournament.Id);

        var semifinals = await GetRoundAsync(tournament.Id, 2);
        Assert.Equal(2, semifinals.Count);
        Assert.All(semifinals, m => Assert.Equal(MatchStatus.Planing, m.Status));

        await CompleteMatchesAsync(semifinals);
        await _bracketService.GenerateNextRoundAsync(tournament.Id);

        var finalMatches = await GetRoundAsync(tournament.Id, 3);
        var finalMatch = Assert.Single(finalMatches);
        Assert.Equal(MatchStatus.Planing, finalMatch.Status);

        await CompleteMatchesAsync(finalMatches);
        await _bracketService.GenerateNextRoundAsync(tournament.Id);

        var bracket = await _brackets.GetAsync(tournament.Id, "playoff");
        Assert.NotNull(bracket);
        Assert.True(bracket.IsCompleted);
        Assert.Equal(finalMatch.HomeTeamId, bracket.ChampionTeamId);
        Assert.Equal(3, bracket.CurrentRound);

        var savedTournament = await _tournaments.GetByIdAsync(tournament.Id);
        Assert.NotNull(savedTournament);
        Assert.Equal(TournamentStatus.Completed, savedTournament.Status);
        Assert.Equal(finalMatch.HomeTeamId, savedTournament.ChampionId);

        var allMatches = await GetMatchesAsync(tournament.Id);
        Assert.Equal(31, allMatches.Count);
        Assert.Equal(24, allMatches.Count(m => m.StageId == "groups"));
        Assert.Equal(7, allMatches.Count(m => m.StageId == "playoff"));
    }
    [Fact]
    public async Task UnfinishedPlayoffRound_ShouldRejectAdvancement()
    {
        var tournament = await CreatePlayoffAsync();
        var before = await _brackets.GetAsync(tournament.Id, "playoff");
        Assert.NotNull(before);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _bracketService.GenerateNextRoundAsync(tournament.Id));

        var after = await _brackets.GetAsync(tournament.Id, "playoff");
        Assert.NotNull(after);
        Assert.Equal(before.CurrentRound, after.CurrentRound);
        Assert.Equal(before.Version, after.Version);
        Assert.Empty(await GetRoundAsync(tournament.Id, 2));
    }

    [Fact]
    public async Task RepeatedAdvancement_ShouldNotCreateDuplicateMatches()
    {
        var tournament = await CreatePlayoffAsync();
        await CompleteMatchesAsync(await GetRoundAsync(tournament.Id, 1));
        await _bracketService.GenerateNextRoundAsync(tournament.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _bracketService.GenerateNextRoundAsync(tournament.Id));

        Assert.Equal(2, (await GetRoundAsync(tournament.Id, 2)).Count);
        Assert.Empty(await GetRoundAsync(tournament.Id, 3));
        Assert.Equal(30, (await GetMatchesAsync(tournament.Id)).Count);
    }

    [Fact]
    public async Task CompletedPlayoff_ShouldReturnSameChampionWithoutNewMatches()
    {
        var tournament = await CreatePlayoffAsync();
        for (int round = 1; round <= 3; round++)
        {
            await CompleteMatchesAsync(await GetRoundAsync(tournament.Id, round));
            await _bracketService.GenerateNextRoundAsync(tournament.Id);
        }

        var before = await _brackets.GetAsync(tournament.Id, "playoff");
        Assert.NotNull(before);
        var countBefore = (await GetMatchesAsync(tournament.Id)).Count;

        await _bracketService.GenerateNextRoundAsync(tournament.Id);

        var after = await _brackets.GetAsync(tournament.Id, "playoff");
        Assert.NotNull(after);
        Assert.True(after.IsCompleted);
        Assert.Equal(before.ChampionTeamId, after.ChampionTeamId);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(countBefore, (await GetMatchesAsync(tournament.Id)).Count);
    }

    [Fact]
    public async Task GroupMatches_ShouldRemainUnchangedDuringPlayoffAdvancement()
    {
        var tournament = await CreatePlayoffAsync();
        var groupsBefore = (await GetMatchesAsync(tournament.Id))
            .Where(m => m.StageId == "groups")
            .ToDictionary(m => m.Id, m => (m.HomeScore, m.AwayScore, m.WinnerId, m.Status));

        await CompleteMatchesAsync(await GetRoundAsync(tournament.Id, 1));
        await _bracketService.GenerateNextRoundAsync(tournament.Id);

        var allMatches = await GetMatchesAsync(tournament.Id);
        var groupsAfter = allMatches.Where(m => m.StageId == "groups").ToList();
        Assert.Equal(24, groupsAfter.Count);
        foreach (var match in groupsAfter)
        {
            Assert.True(groupsBefore.TryGetValue(match.Id, out var original));
            Assert.Equal(original.HomeScore, match.HomeScore);
            Assert.Equal(original.AwayScore, match.AwayScore);
            Assert.Equal(original.WinnerId, match.WinnerId);
            Assert.Equal(original.Status, match.Status);
        }

        Assert.Equal(2, allMatches.Count(m =>
            m.StageId == "playoff" && m.RoundNumber == 2));
    }
}