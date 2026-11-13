using Beckend.Models;
using Beckend.Repositories.Common;
using MongoDB.Driver;

namespace Beckend.Repositories.Matches;

public interface IMatchRepository : IRepository<Match>
{
    Task<List<Match>> GetMatchesByTournamentIdAsync(string tournamentId);
    Task CreateManyAsync(List<Match> matches);
    Task<List<Match>> GetMatchesByTeamIdAsync(string teamId);
    Task<List<Match>> GetUpcomingMatchesAsync();
    Task<List<Match>> GetMatchesByDateRangeAsync(DateTime start, DateTime end);
    Task<Match?> GetMatchByNameAsync(string name);
    Task<List<Match>> GetMatchesByNameSearchAsync(string searchTerm);
    Task CreateManyAsync(IClientSessionHandle session, List<Match> matches);
    Task<bool> CompleteMatchAsync(IClientSessionHandle session, Match match);
    Task<bool> HasUnfinishedMatchesAsync(IClientSessionHandle session, string tournamentId);
    Task<List<Match>> GetSwissMatchesAsync(IClientSessionHandle session, string tournamentId);  

}