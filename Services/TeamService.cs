using Beckend.Models;
using Beckend.Repositories;

namespace Beckend.Services
{
    public class TeamService
    {
        private readonly TeamRepository _teamRepository;

        public TeamService(TeamRepository teamRepository)
        {
            _teamRepository = teamRepository;
        }

        // Отримати команду за назвою
        public async Task<Team?> GetTeamByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Team name is required");

            var team = await _teamRepository.GetTeamByNameAsync(name);

            if (team == null)
                throw new KeyNotFoundException($"Team with name '{name}' not found");

            return team;
        }

        // Пошук команд за назвою (частковий збіг)
        public async Task<List<Team>> SearchTeamsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Team>();

            return await _teamRepository.GetTeamsByNameSearchAsync(searchTerm);
        }

        // Перевірити чи існує команда з такою назвою
        public async Task<bool> TeamNameExistsAsync(string name)
        {
            var team = await _teamRepository.GetTeamByNameAsync(name);
            return team != null;
        }

        // CRUD методи
        public async Task<List<Team>> GetAllAsync() =>
            await _teamRepository.GetAllAsync();

        public async Task<Team?> GetByIdAsync(string id) =>
            await _teamRepository.GetByIdAsync(id);

        public async Task<Team> CreateAsync(Team team)
        {
            // Перевірка унікальності назви
            var exists = await TeamNameExistsAsync(team.TeamName);
            if (exists)
                throw new InvalidOperationException($"Team with name '{team.TeamName}' already exists");

            // Валідація назви
            if (string.IsNullOrWhiteSpace(team.TeamName))
                throw new ArgumentException("Team name is required");

            await _teamRepository.CreateAsync(team);
            return team;
        }

        public async Task<Team> UpdateAsync(string id, Team team)
        {
            var existing = await _teamRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"Team with id {id} not found");

            // Перевірка унікальності назви (якщо назва змінилась)
            if (existing.TeamName != team.TeamName)
            {
                var exists = await TeamNameExistsAsync(team.TeamName);
                if (exists)
                    throw new InvalidOperationException($"Team with name '{team.TeamName}' already exists");
            }

            team.Id = id;
            await _teamRepository.UpdateAsync(id, team);
            return team;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _teamRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            await _teamRepository.DeleteAsync(id);
            return true;
        }

        // Отримати команди з пагінацією
        public async Task<List<Team>> GetTeamsPagedAsync(int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            return await _teamRepository.GetAllTeamsPagedAsync(page, pageSize);
        }
    }
}