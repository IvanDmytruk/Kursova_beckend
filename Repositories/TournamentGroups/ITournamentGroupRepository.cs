using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Repositories.TournamentGroups;

public interface ITournamentGroupRepository
{
    Task CreateManyAsync(
        IClientSessionHandle session,
        List<TournamentGroup> groups);

    Task<List<TournamentGroup>> GetByTournamentIdAsync(
        string tournamentId);
}