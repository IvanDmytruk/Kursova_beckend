using Beckend.Models.Olympic;
using MongoDB.Driver;

namespace Beckend.Repositories.Olympic;

public interface IOlympicBracketRepository
{
    Task<OlympicBracketState?> GetAsync(
        string tournamentId,
        string stageId);

    Task CreateAsync(OlympicBracketState state);

    Task UpdateAsync(OlympicBracketState state);

    Task<bool> TryUpdateAsync(
        OlympicBracketState state,
        long expectedVersion);

    Task CreateAsync(
        IClientSessionHandle session,
        OlympicBracketState state);

    Task<bool> TryUpdateAsync(
        IClientSessionHandle session,
        OlympicBracketState state,
        long expectedVersion);
}
