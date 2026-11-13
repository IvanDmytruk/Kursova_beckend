using Beckend.Models;
using Beckend.Repositories.Swiss;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Beckend.Tests.Integration;

public class SwissByeRepositoryIntegrationTests
    : IClassFixture<MongoDbFixture>
{
    private readonly MongoDbFixture _fixture;

    public SwissByeRepositoryIntegrationTests(
        MongoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateBye_ShouldPersistBye()
    {
        var repository = new SwissByeRepository(
            _fixture.Database);

        var bye = new SwissBye
        {
            TournamentId = ObjectId.GenerateNewId().ToString(),
            TeamId = ObjectId.GenerateNewId().ToString(),
            RoundNumber = 1,
            PointsAwarded = 3,
            CountsAsWin = false
        };

        using var session =
            await _fixture.Database.Client.StartSessionAsync();

        session.StartTransaction();

        await repository.CreateByeAsync(session, bye);

        await session.CommitTransactionAsync();

        using var readSession =
            await _fixture.Database.Client.StartSessionAsync();

        var saved = await repository.GetByTournamentAsync(
            readSession,
            bye.TournamentId);

        Assert.Single(saved);
        Assert.Equal(bye.TeamId, saved[0].TeamId);
        Assert.Equal(1, saved[0].RoundNumber);
        Assert.Equal(3m, saved[0].PointsAwarded);
        Assert.False(saved[0].CountsAsWin);
    }

    [Fact]
    public async Task CreateBye_AbortedTransaction_ShouldNotPersist()
    {
        var repository = new SwissByeRepository(
            _fixture.Database);

        var bye = new SwissBye
        {
            TournamentId = ObjectId.GenerateNewId().ToString(),
            TeamId = ObjectId.GenerateNewId().ToString(),
            RoundNumber = 1,
            PointsAwarded = 3,
            CountsAsWin = true
        };

        using var session =
            await _fixture.Database.Client.StartSessionAsync();

        session.StartTransaction();

        await repository.CreateByeAsync(session, bye);

        await session.AbortTransactionAsync();

        using var readSession =
            await _fixture.Database.Client.StartSessionAsync();

        var saved = await repository.GetByTournamentAsync(
            readSession,
            bye.TournamentId);

        Assert.Empty(saved);
    }

    [Fact]
    public async Task GetByTournament_ShouldReturnOnlyItsByes()
    {
        var repository = new SwissByeRepository(
            _fixture.Database);

        string tournamentA =
            ObjectId.GenerateNewId().ToString();

        string tournamentB =
            ObjectId.GenerateNewId().ToString();

        using var session =
            await _fixture.Database.Client.StartSessionAsync();

        session.StartTransaction();

        await repository.CreateByeAsync(
            session,
            new SwissBye
            {
                TournamentId = tournamentA,
                TeamId = ObjectId.GenerateNewId().ToString(),
                RoundNumber = 1,
                PointsAwarded = 3
            });

        await repository.CreateByeAsync(
            session,
            new SwissBye
            {
                TournamentId = tournamentA,
                TeamId = ObjectId.GenerateNewId().ToString(),
                RoundNumber = 2,
                PointsAwarded = 3
            });

        await repository.CreateByeAsync(
            session,
            new SwissBye
            {
                TournamentId = tournamentB,
                TeamId = ObjectId.GenerateNewId().ToString(),
                RoundNumber = 1,
                PointsAwarded = 3
            });

        await session.CommitTransactionAsync();

        using var readSession =
            await _fixture.Database.Client.StartSessionAsync();

        var result = await repository.GetByTournamentAsync(
            readSession,
            tournamentA);

        Assert.Equal(2, result.Count);

        Assert.All(result, bye =>
            Assert.Equal(tournamentA, bye.TournamentId));
    }
}
