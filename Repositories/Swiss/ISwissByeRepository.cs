
using Beckend.Models;
using Beckend.Repositories.Common;
using MongoDB.Driver;

namespace Beckend.Repositories.Swiss;

public interface ISwissByeRepository : IRepository<SwissBye>
{
    Task<List<SwissBye>> GetByTournamentAsync(
        IClientSessionHandle session,
        string tournamentId);

    Task CreateByeAsync(
        IClientSessionHandle session,
        SwissBye bye);
}
