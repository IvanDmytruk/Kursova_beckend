using Beckend.Models;
using Beckend.Repositories;

namespace Beckend.Services
{
    public class MatchService
    {
        private readonly MatchRepository _matchRepository;

        public MatchService(MatchRepository matchRepository)
        {
            _matchRepository = matchRepository;
        }

        // Отримати матч за назвою
        public async Task<Match?> GetMatchByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Match name is required");

            var match = await _matchRepository.GetMatchByNameAsync(name);

            if (match == null)
                throw new KeyNotFoundException($"Match with name '{name}' not found");

            return match;
        }

        // Отримати матчі турніру
        public async Task<List<Match>> GetMatchesByTournamentIdAsync(string tournamentId)
        {
            if (string.IsNullOrWhiteSpace(tournamentId))
                throw new ArgumentException("Tournament ID is required");

            return await _matchRepository.GetMatchesByTournamentIdAsync(tournamentId);
        }

        // Отримати матчі команди
        public async Task<List<Match>> GetMatchesByTeamIdAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID is required");

            return await _matchRepository.GetMatchesByTeamIdAsync(teamId);
        }

        // Отримати майбутні матчі
        public async Task<List<Match>> GetUpcomingMatchesAsync()
        {
            return await _matchRepository.GetUpcomingMatchesAsync();
        }

        // Отримати матчі за діапазоном дат - ВИПРАВЛЕНО НАЗВУ МЕТОДУ
        public async Task<List<Match>> GetMatchesByDateRangeAsync(DateTime start, DateTime end)
        {
            if (start > end)
                throw new ArgumentException("Start date must be before end date");

            return await _matchRepository.GetMatchesByDateRangeAsync(start, end); // Виправлено назву
        }

        // Перевірити чи існує матч
        public async Task<bool> MatchExistsAsync(string name)
        {
            var match = await _matchRepository.GetMatchByNameAsync(name);
            return match != null;
        }

        // CRUD методи
        public async Task<List<Match>> GetAllAsync() =>
            await _matchRepository.GetAllAsync();

        public async Task<Match?> GetByIdAsync(string id) =>
            await _matchRepository.GetByIdAsync(id);

        public async Task<Match> CreateAsync(Match match)
        {
            // Перевірка унікальності назви
            var exists = await MatchExistsAsync(match.MatchName);
            if (exists)
                throw new InvalidOperationException($"Match with name '{match.MatchName}' already exists");

            // Валідація
            if (string.IsNullOrWhiteSpace(match.MatchName))
                throw new ArgumentException("Match name is required");

            if (match.StartTime < DateTime.UtcNow)
                throw new ArgumentException("Start time cannot be in the past");

            if (match.TicketCost < 0)
                throw new ArgumentException("Ticket cost cannot be negative");

            if (match.MaxViewers <= 0)
                throw new ArgumentException("Max viewers must be greater than 0");

            if (string.IsNullOrWhiteSpace(match.HomeTeamId))
                throw new ArgumentException("Home team is required");

            if (string.IsNullOrWhiteSpace(match.AwayTeamId))
                throw new ArgumentException("Away team is required");

            if (match.HomeTeamId == match.AwayTeamId)
                throw new ArgumentException("Home team and away team cannot be the same");

            await _matchRepository.CreateAsync(match);
            return match;
        }

        public async Task<Match> UpdateAsync(string id, Match match)
        {
            var existing = await _matchRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"Match with id {id} not found");

            // Перевірка унікальності назви (якщо назва змінилась)
            if (existing.MatchName != match.MatchName)
            {
                var exists = await MatchExistsAsync(match.MatchName);
                if (exists)
                    throw new InvalidOperationException($"Match with name '{match.MatchName}' already exists");
            }

            match.Id = id;
            await _matchRepository.UpdateAsync(id, match);
            return match;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _matchRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            await _matchRepository.DeleteAsync(id);
            return true;
        }

        // Отримати матчі, які скоро почнуться (наступні 24 години)
        public async Task<List<Match>> GetMatchesStartingSoonAsync(int hours = 24)
        {
            var now = DateTime.UtcNow;
            var soon = now.AddHours(hours);

            return await _matchRepository.GetMatchesByDateRangeAsync(now, soon); // Виправлено назву
        }
    }
}