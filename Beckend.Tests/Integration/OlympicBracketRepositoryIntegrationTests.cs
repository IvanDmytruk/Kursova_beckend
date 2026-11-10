
using Beckend.Models.Olympic;
using Beckend.Repositories;
using Xunit;

namespace Beckend.Tests.Integration;

public class OlympicBracketRepositoryIntegrationTests
    : IClassFixture<MongoDbFixture>
{
    private readonly OlympicBracketRepository _repository;

    public OlympicBracketRepositoryIntegrationTests(
        MongoDbFixture fixture)
    {
        _repository = new OlympicBracketRepository(
            fixture.Database);
    }

    private static OlympicBracketState CreateState()
    {
        return new OlympicBracketState
        {
            TournamentId = Guid.NewGuid().ToString(),
            StageId = "main",
            ParticipantCount = 4,
            BracketSize = 4,
            CurrentRound = 1,
            Slots = new List<string?>
            {
                "A", "D", "B", "C"
            },
            Version = 0
        };
    }

    [Fact]
    public async Task CreateAsync_ShouldSaveBracket()
    {
        var state = CreateState();

        await _repository.CreateAsync(state);

        var saved = await _repository.GetAsync(
            state.TournamentId, state.StageId);

        Assert.NotNull(saved);
        Assert.Equal(state.Id, saved.Id);
        Assert.Equal(4, saved.BracketSize);
        Assert.Equal(1, saved.CurrentRound);
    }

    [Fact]
    public async Task TryUpdateAsync_ShouldIncreaseVersion()
    {
        var state = CreateState();

        await _repository.CreateAsync(state);

        state.CurrentRound = 2;

        bool updated = await _repository.TryUpdateAsync(
            state, expectedVersion: 0);

        var saved = await _repository.GetAsync(
            state.TournamentId, state.StageId);

        Assert.True(updated);
        Assert.NotNull(saved);
        Assert.Equal(2, saved.CurrentRound);
        Assert.Equal(1, saved.Version);
    }

    [Fact]
    public async Task TryUpdateAsync_WithOldVersion_ShouldFail()
    {
        var state = CreateState();

        await _repository.CreateAsync(state);

        state.CurrentRound = 2;

        bool firstUpdate = await _repository.TryUpdateAsync(
            state, expectedVersion: 0);

        state.CurrentRound = 3;

        bool secondUpdate = await _repository.TryUpdateAsync(
            state, expectedVersion: 0);

        var saved = await _repository.GetAsync(
            state.TournamentId, state.StageId);

        Assert.True(firstUpdate);
        Assert.False(secondUpdate);
        Assert.NotNull(saved);
        Assert.Equal(2, saved.CurrentRound);
        Assert.Equal(1, saved.Version);
    }

    [Fact]
    public async Task CreateAsync_DuplicateBracket_ShouldFail()
    {
        var first = CreateState();

        var duplicate = CreateState();
        duplicate.TournamentId = first.TournamentId;
        duplicate.StageId = first.StageId;

        await _repository.CreateAsync(first);

        await Assert.ThrowsAsync<MongoDB.Driver.MongoWriteException>(
            () => _repository.CreateAsync(duplicate));
    }

    [Fact]
    public async Task GetAsync_UnknownTournament_ShouldReturnNull()
    {
        var result = await _repository.GetAsync(
            Guid.NewGuid().ToString(), "main");

        Assert.Null(result);
    }
}
