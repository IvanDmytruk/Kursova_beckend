using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Repositories
{
    public class MatchRepository : BaseRepository<Match>
    {
        protected readonly IMongoCollection<Match> _matches;

        public MatchRepository(IConfiguration config) : base(config, "Matches") { }

        // Отримати матчі за турніром
        public async Task<List<Match>> GetMatchesByTournamentIdAsync(string tournamentId)
        {
            if (string.IsNullOrWhiteSpace(tournamentId))
                throw new ArgumentException("Tournament ID cannot be empty", nameof(tournamentId));

            return await _matches
                .Find(m => m.TournamentId == tournamentId)
                .ToListAsync();
        }

        // Отримати матчі за командою (домашні або гостьові)
        public async Task<List<Match>> GetMatchesByTeamIdAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID cannot be empty", nameof(teamId));

            var filter = Builders<Match>.Filter
                .Where(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId);

            return await _matches.Find(filter).ToListAsync();
        }

        // Отримати майбутні матчі
        public async Task<List<Match>> GetUpcomingMatchesAsync()
        {
            return await _matches
                .Find(m => m.StartTime > DateTime.UtcNow)
                .SortBy(m => m.StartTime)
                .ToListAsync();
        }

        // Отримати матчі за діапазоном дат
        public async Task<List<Match>> GetMatchesByDateRangeAsync(DateTime start, DateTime end)
        {
            var filter = Builders<Match>.Filter
                .Where(m => m.StartTime >= start && m.StartTime <= end);

            return await _matches.Find(filter).ToListAsync();
        }

        // Пошук за назвою матчу
        public async Task<Match?> GetMatchByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Match name cannot be empty", nameof(name));

            return await _matches
                .Find(m => m.MatchName == name)
                .FirstOrDefaultAsync();
        }
    }
}