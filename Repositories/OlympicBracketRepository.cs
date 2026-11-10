
using Beckend.Models.Olympic;
using MongoDB.Driver;

namespace Beckend.Repositories;

public class OlympicBracketRepository
{
    private readonly IMongoCollection<OlympicBracketState>
        _collection;

    public OlympicBracketRepository(IMongoDatabase database)
    {
        _collection = database
            .GetCollection<OlympicBracketState>(
                "OlympicBrackets");

        var indexKeys =
            Builders<OlympicBracketState>.IndexKeys
                .Ascending(x => x.TournamentId)
                .Ascending(x => x.StageId);

        _collection.Indexes.CreateOne(
            new CreateIndexModel<OlympicBracketState>(
                indexKeys,
                new CreateIndexOptions { Unique = true }));
    }

    public async Task<OlympicBracketState?> GetAsync(string tournamentId,string stageId)
    {
        return await _collection.Find(x =>
                x.TournamentId == tournamentId &&
                x.StageId == stageId)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(OlympicBracketState state)
    {
        await _collection.InsertOneAsync(state);
    }

    public async Task UpdateAsync(OlympicBracketState state)
    {
        await _collection.ReplaceOneAsync(
            x => x.Id == state.Id,
            state);
    }

    public async Task<bool> TryUpdateAsync(OlympicBracketState state,long expectedVersion)
    {
        var filter = Builders<OlympicBracketState>
            .Filter.And(
                Builders<OlympicBracketState>
                    .Filter.Eq(x => x.Id, state.Id),
                Builders<OlympicBracketState>
                    .Filter.Eq(
                        x => x.Version,
                        expectedVersion)
            );

        var updatedState = new OlympicBracketState
        {
            Id = state.Id,
            TournamentId = state.TournamentId,
            StageId = state.StageId,
            ParticipantCount = state.ParticipantCount,
            BracketSize = state.BracketSize,
            CurrentRound = state.CurrentRound,
            Slots = state.Slots,
            IsCompleted = state.IsCompleted,
            ChampionTeamId = state.ChampionTeamId,
            Version = expectedVersion + 1
        };

        var result = await _collection.ReplaceOneAsync(
            filter,
            updatedState);

        if (result.ModifiedCount == 1)
        {
            state.Version = updatedState.Version;
            return true;
        }

        return false;
    }

    public async Task CreateAsync( IClientSessionHandle session,OlympicBracketState state)
    {
        await _collection.InsertOneAsync(session, state);
    }

    public async Task<bool> TryUpdateAsync(
        IClientSessionHandle session,
        OlympicBracketState state,
        long expectedVersion)
    {
        var filter = Builders<OlympicBracketState>.Filter.And(
            Builders<OlympicBracketState>.Filter.Eq(
                x => x.Id, state.Id),
            Builders<OlympicBracketState>.Filter.Eq(
                x => x.Version, expectedVersion));

        var update = Builders<OlympicBracketState>.Update
            .Set(x => x.CurrentRound, state.CurrentRound)
            .Set(x => x.IsCompleted, state.IsCompleted)
            .Set(x => x.ChampionTeamId, state.ChampionTeamId)
            .Inc(x => x.Version, 1);

        var result = await _collection.UpdateOneAsync(
            session, filter, update);

        return result.ModifiedCount == 1;
    }


}
