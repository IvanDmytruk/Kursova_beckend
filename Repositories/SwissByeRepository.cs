using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Repositories;

public class SwissByeRepository : BaseRepository<SwissBye>
{
    public SwissByeRepository(IMongoDatabase database)
        : base(database, "SwissByes")
    {
    }

    public async Task<List<SwissBye>> GetByTournamentAsync(
        IClientSessionHandle session,
        string tournamentId)
    {
        return await _collection
            .Find(session, b => b.TournamentId == tournamentId)
            .ToListAsync();
    }

    public async Task CreateByeAsync(
        IClientSessionHandle session,
        SwissBye bye)
    {
        await _collection.InsertOneAsync(session, bye);
    }
}
