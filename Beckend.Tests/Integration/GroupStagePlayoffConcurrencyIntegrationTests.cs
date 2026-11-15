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

public class GroupStagePlayoffConcurrencyIntegrationTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly GroupStagePlayoffService _playoff;
    private readonly OlympicBracketService _bracketService;
    private readonly TournamentRepository _tournaments;
    private readonly MatchRepository _matches;
    private readonly OlympicBracketRepository _brackets;

    public GroupStagePlayoffConcurrencyIntegrationTests(MongoDbFixture fixture)
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

    private static async Task<(int Succeeded, int Failed)> RunConcurrentlyAsync(
        Func<Task> operation)
    {
        var start = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Exception?> AttemptAsync()
        {
            await start.Task;
            try
            {
                await operation();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        var first = Task.Run(AttemptAsync);
        var second = Task.Run(AttemptAsync);
        start.SetResult(true);

        var results = await Task.WhenAll(first, second);
        return (results.Count(e => e is null),
                results.Count(e => e is not null));
    }

    [Fact]
    public async Task ConcurrentPlayoffGeneration_ShouldPersistOneBracketAndFourMatches()
    {
        var tournament = await CreateTournamentAsync();
        await CompleteMatchesAsync((await GetMatchesAsync(tournament.Id))
            .Where(m => m.StageId == "groups"));

        var result = await RunConcurrentlyAsync(() =>
            _playoff.GenerateAsync(tournament.Id));

        Assert.Equal(1, result.Succeeded);
        Assert.Equal(1, result.Failed);

        var bracket = await _brackets.GetAsync(tournament.Id, "playoff");
        Assert.NotNull(bracket);
        Assert.Equal(1, bracket.CurrentRound);

        var playoff = (await GetMatchesAsync(tournament.Id))
            .Where(m => m.StageId == "playoff").ToList();
        Assert.Equal(4, playoff.Count);
        Assert.All(playoff, m => Assert.Equal(1, m.RoundNumber));
        Assert.Equal(28, (await GetMatchesAsync(tournament.Id)).Count);
    }

    [Fact]
    public async Task ConcurrentRoundAdvancement_ShouldPersistOnlyOneNextRound()
    {
        var tournament = await CreatePlayoffAsync();
        await CompleteMatchesAsync(await GetRoundAsync(tournament.Id, 1));

        var result = await RunConcurrentlyAsync(() =>
            _bracketService.GenerateNextRoundAsync(tournament.Id));

        Assert.Equal(1, result.Succeeded);
        Assert.Equal(1, result.Failed);

        var bracket = await _brackets.GetAsync(tournament.Id, "playoff");
        Assert.NotNull(bracket);
        Assert.Equal(2, bracket.CurrentRound);
        Assert.Equal(2, (await GetRoundAsync(tournament.Id, 2)).Count);
        Assert.Empty(await GetRoundAsync(tournament.Id, 3));
        Assert.Equal(30, (await GetMatchesAsync(tournament.Id)).Count);
    }
}
