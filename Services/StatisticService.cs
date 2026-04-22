using Beckend.Models;
using Beckend.Repositories;

namespace Beckend.Services
{
    public class StatisticService
    {
        private readonly StatisticRepository _statisticRepository;

        public StatisticService(StatisticRepository statisticRepository)
        {
            _statisticRepository = statisticRepository;
        }

        // Отримати статистику гравця
        public async Task<List<Statistic>> GetStatisticsByUserIdAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("User ID is required");

            return await _statisticRepository.GetStatisticsByUserIdAsync(userId);
        }

        // Отримати статистику команди
        public async Task<List<Statistic>> GetStatisticsByTeamIdAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID is required");

            return await _statisticRepository.GetStatisticsByTeamIdAsync(teamId);
        }

        // Отримати статистику турніру
        public async Task<List<Statistic>> GetStatisticsByTournamentIdAsync(string tournamentId)
        {
            if (string.IsNullOrWhiteSpace(tournamentId))
                throw new ArgumentException("Tournament ID is required");

            return await _statisticRepository.GetStatisticsByTournamentIdAsync(tournamentId);
        }

        // Отримати статистику за сезоном
        public async Task<List<Statistic>> GetStatisticsBySeasonAsync(string season)
        {
            if (string.IsNullOrWhiteSpace(season))
                return new List<Statistic>();

            return await _statisticRepository.GetStatisticsBySeasonAsync(season);
        }

        // Отримати топ гравців за очками
        public async Task<List<Statistic>> GetTopPlayersAsync(int limit = 10)
        {
            if (limit < 1) limit = 10;

            return await _statisticRepository.GetTopPlayersByPointsAsync(limit);
        }

        // CRUD методи
        public async Task<List<Statistic>> GetAllAsync() =>
            await _statisticRepository.GetAllAsync();

        public async Task<Statistic?> GetByIdAsync(string id) =>
            await _statisticRepository.GetByIdAsync(id);

        public async Task<Statistic> CreateAsync(Statistic statistic)
        {
            // Валідація
            if (string.IsNullOrWhiteSpace(statistic.UserId))
                throw new ArgumentException("User ID is required");

            if (string.IsNullOrWhiteSpace(statistic.TeamId))
                throw new ArgumentException("Team ID is required");

            if (string.IsNullOrWhiteSpace(statistic.TournamentId))
                throw new ArgumentException("Tournament ID is required");

            if (statistic.Wins < 0 || statistic.Losses < 0 || statistic.Draws < 0)
                throw new ArgumentException("Statistics cannot be negative");

            await _statisticRepository.CreateAsync(statistic);
            return statistic;
        }

        public async Task<Statistic> UpdateAsync(string id, Statistic statistic)
        {
            var existing = await _statisticRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"Statistic with id {id} not found");

            statistic.Id = id;
            await _statisticRepository.UpdateAsync(id, statistic);
            return statistic;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _statisticRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            await _statisticRepository.DeleteAsync(id);
            return true;
        }

        // Оновити статистику після матчу
        public async Task<Statistic> UpdateMatchStatsAsync(string id, bool isWin, bool isDraw = false)
        {
            var statistic = await _statisticRepository.GetByIdAsync(id);
            if (statistic == null)
                throw new KeyNotFoundException($"Statistic with id {id} not found");

            statistic.MatchesPlayed++;

            if (isWin)
            {
                statistic.Wins++;
                statistic.Points += 3;
            }
            else if (isDraw)
            {
                statistic.Draws++;
                statistic.Points += 1;
            }
            else
            {
                statistic.Losses++;
            }

            await _statisticRepository.UpdateAsync(id, statistic);
            return statistic;
        }
    }
}