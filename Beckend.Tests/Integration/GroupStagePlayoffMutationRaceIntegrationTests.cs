using Beckend.Enums;
using Beckend.Models;
using Beckend.Models.Olympic;
using Beckend.Repositories.Matches;
using Beckend.Repositories.Olympic;
using Beckend.Repositories.TournamentAdministration;
using Beckend.Repositories.TournamentGroups;
using Beckend.Repositories.Tournaments;
using Beckend.Services.TournamentGenerators;
using Beckend.Services.TournamentStandings;
using MongoDB.Driver;
using Xunit;

namespace Beckend.Tests.Integration;

public class GroupStagePlayoffMutationRaceIntegrationTests : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;
    private readonly GroupStagePlayoffService _playoff;
    private readonly MatchRepository _matches;
    private readonly OlympicBracketRepository _brackets;

    public GroupStagePlayoffMutationRaceIntegrationTests(MongoDbFixture fixture)
    {
        _fixture = fixture;
        var tournaments = new TournamentRepository(fixture.Database);
        _matches = new MatchRepository(fixture.Database);
        var groups = new TournamentGroupRepository(fixture.Database);
        _brackets = new OlympicBracketRepository(fixture.Database);
        var administration = new TournamentAdministrationRepository(fixture.Database);
        _playoff = new GroupStagePlayoffService(
            tournaments, _matches, groups, _brackets,
            new GroupStageStandingsGenerator(
                groups, new RoundRobinStandingsGenerator(), administration),
            new OlympicGenerator(), fixture.Database.Client, administration);
    }

    [Fact]
    public async Task ConcurrentDisqualificationAndPlayoffGeneration_ShouldNotLeavePartialPlayoff()
    {
        var tournament = new Tournament
        {
            TournamentName = $"Mutation race {Guid.NewGuid():N}",
            TournamentType = TournamentType.GroupStage,
            Status = TournamentStatus.InProgress,
            TournamentParticipants = Enumerable.Range(1, 16)
                .Select(i => new Team { TeamName = $"Team {i}" }).ToList(),
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

        var matchesCollection = _fixture.Database.GetCollection<Match>("Matches");
        foreach (var match in generated.Matches)
        {
            var update = Builders<Match>.Update
                .Set(m => m.HomeScore, 2)
                .Set(m => m.AwayScore, 0)
                .Set(m => m.WinnerId, match.HomeTeamId)
                .Set(m => m.Status, MatchStatus.Completed);
            await matchesCollection.UpdateOneAsync(m => m.Id == match.Id, update);
        }

        // Three disqualifications leave only one eligible team in a group:
        // a generation observing these changes must reject the playoff.
        var firstGroup = generated.Groups[0];
        var states = firstGroup.TeamIds.Take(3)
            .Select(teamId => new TournamentParticipantState
            {
                TournamentId = tournament.Id,
                TeamId = teamId,
                Status = TournamentParticipantStatus.Disqualified,
                Reason = "Concurrent integration test",
                UpdatedBy = "test-admin",
                UpdatedAt = DateTime.UtcNow
            }).ToList();

        var start = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var generateTask = Task.Run(async () =>
        {
            await start.Task;
            try
            {
                await _playoff.GenerateAsync(tournament.Id);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (MongoException)
            {
                // A concurrent transaction conflict may abort generation.
                return false;
            }
        });

        var disqualifyTask = Task.Run(async () =>
        {
            await start.Task;
            await _fixture.Database
                .GetCollection<TournamentParticipantState>("TournamentParticipantStates")
                .InsertManyAsync(states);
        });

        start.SetResult(true);
        await Task.WhenAll(generateTask, disqualifyTask);

        var bracket = await _brackets.GetAsync(tournament.Id, "playoff");
        var allMatches = await _matches.GetMatchesByTournamentIdAsync(tournament.Id);
        var playoffMatches = allMatches.Where(m => m.StageId == "playoff").ToList();

        Assert.Equal(24, allMatches.Count(m => m.StageId == "groups"));
        Assert.Equal(3, states.Count);

        if (await generateTask)
        {
            // The generator may have read the earlier valid snapshot.
            Assert.NotNull(bracket);
            Assert.Equal(8, bracket.ParticipantCount);
            Assert.Equal(1, bracket.CurrentRound);
            Assert.Equal(4, playoffMatches.Count);
            Assert.Equal(8, bracket.Slots.Where(id => id != null).Distinct().Count());
        }
        else
        {
            // A rejected/aborted generation must not persist half a bracket.
            Assert.Null(bracket);
            Assert.Empty(playoffMatches);
        }
    }
}
