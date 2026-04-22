using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Repositories
{
    public class StatisticRepository : BaseRepository<Statistic>
    {
        protected readonly IMongoCollection<Statistic> _statistics;

        public StatisticRepository(IConfiguration config) : base(config, "Statistics") { }

        // Отримати статистику за UserId
        public async Task<List<Statistic>> GetStatisticsByUserIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID cannot be empty", nameof(userId));

            return await _statistics
                .Find(s => s.UserId == userId)
                .ToListAsync();
        }

        // Отримати статистику за TeamId
        public async Task<List<Statistic>> GetStatisticsByTeamIdAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID cannot be empty", nameof(teamId));

            return await _statistics
                .Find(s => s.TeamId == teamId)
                .ToListAsync();
        }

        // Отримати статистику за TournamentId
        public async Task<List<Statistic>> GetStatisticsByTournamentIdAsync(string tournamentId)
        {
            if (string.IsNullOrWhiteSpace(tournamentId))
                throw new ArgumentException("Tournament ID cannot be empty", nameof(tournamentId));

            return await _statistics
                .Find(s => s.TournamentId == tournamentId)
                .ToListAsync();
        }

        // Отримати статистику за сезоном
        public async Task<List<Statistic>> GetStatisticsBySeasonAsync(string season)
        {
            if (string.IsNullOrWhiteSpace(season))
                return new List<Statistic>();

            return await _statistics
                .Find(s => s.Season == season)
                .ToListAsync();
        }

        // Отримати топ гравців за очками
        public async Task<List<Statistic>> GetTopPlayersByPointsAsync(int limit)
        {
            return await _statistics
                .Find(_ => true)
                .SortByDescending(s => s.Points)
                .Limit(limit)
                .ToListAsync();
        }
    }
}