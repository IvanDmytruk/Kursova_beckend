using Beckend.Models;
using MongoDB.Driver;
using Beckend.Repositories.Common;
namespace Beckend.Repositories.Statistics
{
    public class StatisticRepository : BaseRepository<Statistic>, IStatisticRepository
    {
        public StatisticRepository(IMongoDatabase database) : base(database, "Statistics") { }
        public async Task<List<Statistic>> GetStatisticsByUserIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be empty", nameof(userId));

            return await _collection
                .Find(s => s.UserId == userId)
                .ToListAsync();
        }

        public async Task<List<Statistic>> GetStatisticsByTeamIdAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID cannot be empty", nameof(teamId));

            return await _collection
                .Find(s => s.TeamId == teamId)
                .ToListAsync();
        }

        public async Task<List<Statistic>> GetStatisticsByTournamentIdAsync(string tournamentId)
        {
            if (string.IsNullOrWhiteSpace(tournamentId))
                throw new ArgumentException("Tournament ID cannot be empty", nameof(tournamentId));

            return await _collection
                .Find(s => s.TournamentId == tournamentId)
                .ToListAsync();
        }
        public async Task<List<Statistic>> GetStatisticsBySeasonAsync(string season)
        {
            if (string.IsNullOrWhiteSpace(season))
                return new List<Statistic>();

            return await _collection
                .Find(s => s.Season == season)
                .ToListAsync();
        }
        public async Task<List<Statistic>> GetTopPlayersByPointsAsync(int limit)
        {
            return await _collection
                .Find(_ => true)
                .SortByDescending(s => s.Points)
                .Limit(limit)
                .ToListAsync();
        }
    }
}