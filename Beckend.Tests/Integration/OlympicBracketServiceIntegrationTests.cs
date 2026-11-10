
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories;
using Beckend.Services.Olympic;
using Beckend.Services.TournamentGenerators;
using MongoDB.Driver;
using Xunit;

namespace Beckend.Tests.Integration;

public class OlympicBracketServiceIntegrationTests
    : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;

    public OlympicBracketServiceIntegrationTests(
        MongoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GenerateFirstRound_ShouldStartTournament()
    {
        var database = _fixture.Database;

        var tournaments = new TournamentRepository(database);
        var matches = new MatchRepository(database);
        var brackets = new OlympicBracketRepository(database);

        var service = new OlympicBracketService(
            tournaments,
            matches,
            brackets,
            new OlympicGenerator(),
            database.Client);

        var tournament = new Tournament
        {
            TournamentName = "Olympic Integration Test",
            TournamentType = TournamentType.OlympicSystem,
            Status = TournamentStatus.Ready,
            TournamentParticipants = new List<Team>
            {
                new Team(),
                new Team(),
                new Team(),
                new Team()
            },
            Settings = new TournamentSettings
            {
                ShuffleParticipants = false
            }
        };

        await database
            .GetCollection<Tournament>("Tournaments")
            .InsertOneAsync(tournament);

        var response =
            await service.GenerateFirstRoundAsync(tournament.Id);

        var savedTournament =
            await tournaments.GetByIdAsync(tournament.Id);

        var savedMatches =
            await matches.GetMatchesByTournamentIdAsync(
                tournament.Id);

        var savedBracket =
            await brackets.GetAsync(tournament.Id, "main");

        Assert.NotNull(savedTournament);
        Assert.Equal(
            TournamentStatus.InProgress,
            savedTournament.Status);

        Assert.NotNull(savedBracket);
        Assert.Equal(1, savedBracket.CurrentRound);

        Assert.Equal(2, savedMatches.Count);
        Assert.Equal(2, response.MatchesCount);

        Assert.All(savedMatches, match =>
        {
            Assert.Equal(1, match.RoundNumber);
            Assert.Equal("main", match.StageId);
            Assert.True(match.IsAutomatic);
        });
    }
}
