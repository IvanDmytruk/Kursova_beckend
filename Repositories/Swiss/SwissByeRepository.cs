using Beckend.Models;
using MongoDB.Driver;
using Beckend.Repositories.Common;

namespace Beckend.Repositories.Swiss;

public class SwissByeRepository : BaseRepository<SwissBye>, ISwissByeRepository
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
