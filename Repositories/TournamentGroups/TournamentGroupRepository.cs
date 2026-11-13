using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Repositories.TournamentGroups;

public class TournamentGroupRepository : ITournamentGroupRepository
{
    private readonly IMongoCollection<TournamentGroup> _collection;

    public TournamentGroupRepository(IMongoDatabase database)
    {
        _collection = database
            .GetCollection<TournamentGroup>("TournamentGroups");

        var keys = Builders<TournamentGroup>.IndexKeys
            .Ascending(g => g.TournamentId)
            .Ascending(g => g.GroupId);

        _collection.Indexes.CreateOne(
            new CreateIndexModel<TournamentGroup>(
                keys,
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_tournament_group"
                }));
    }

    public async Task CreateManyAsync(
        IClientSessionHandle session,
        List<TournamentGroup> groups)
    {
        if (groups.Count == 0)
            return;

        await _collection.InsertManyAsync(session, groups);
    }

    public async Task<List<TournamentGroup>> GetByTournamentIdAsync(
        string tournamentId)
    {
        return await _collection
            .Find(g => g.TournamentId == tournamentId)
            .SortBy(g => g.GroupId)
            .ToListAsync();
    }
}