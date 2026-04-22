using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories;

namespace Beckend.Services
{
    public class SportService
    {
        private readonly SportRepository _sportRepository;

        public SportService(SportRepository sportRepository)
        {
            _sportRepository = sportRepository;
        }

        // Отримати вид спорту за назвою
        public async Task<Sport?> GetSportByNameAsync(SportName sportName)
        {
            var sport = await _sportRepository.GetSportByNameAsync(sportName);

            if (sport == null)
                throw new KeyNotFoundException($"Sport with name '{sportName}' not found");

            return sport;
        }

        // Отримати види спорту за типом
        public async Task<List<Sport>> GetSportsByTypeAsync(TypeSport type)
        {
            return await _sportRepository.GetSportsByTypeAsync(type);
        }

        // Отримати активні види спорту
        public async Task<List<Sport>> GetActiveSportsAsync()
        {
            return await _sportRepository.GetActiveSportsAsync();
        }

        // Пошук за описом
        public async Task<List<Sport>> SearchSportsByDescriptionAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Sport>();

            return await _sportRepository.GetSportsByDescriptionSearchAsync(searchTerm);
        }

        // Перевірити чи існує вид спорту
        public async Task<bool> SportExistsAsync(SportName sportName)
        {
            var sport = await _sportRepository.GetSportByNameAsync(sportName);
            return sport != null;
        }

        // CRUD методи
        public async Task<List<Sport>> GetAllAsync() =>
            await _sportRepository.GetAllAsync();

        public async Task<Sport?> GetByIdAsync(string id) =>
            await _sportRepository.GetByIdAsync(id);

        public async Task<Sport> CreateAsync(Sport sport)
        {
            // Перевірка унікальності
            var exists = await SportExistsAsync(sport.SportName);
            if (exists)
                throw new InvalidOperationException($"Sport with name '{sport.SportName}' already exists");

            // Валідація
            if (string.IsNullOrWhiteSpace(sport.SportDescription))
                sport.SportDescription = string.Empty;

            sport.CreatedAt = DateTime.UtcNow;
            sport.IsActive = true;

            await _sportRepository.CreateAsync(sport);
            return sport;
        }

        public async Task<Sport> UpdateAsync(string id, Sport sport)
        {
            var existing = await _sportRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"Sport with id {id} not found");

            // Перевірка унікальності назви (якщо назва змінилась)
            if (existing.SportName != sport.SportName)
            {
                var exists = await SportExistsAsync(sport.SportName);
                if (exists)
                    throw new InvalidOperationException($"Sport with name '{sport.SportName}' already exists");
            }

            sport.Id = id;
            await _sportRepository.UpdateAsync(id, sport);
            return sport;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _sportRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            await _sportRepository.DeleteAsync(id);
            return true;
        }

        // Активація/деактивація виду спорту
        public async Task<Sport> ToggleSportActiveAsync(string id)
        {
            var sport = await _sportRepository.GetByIdAsync(id);
            if (sport == null)
                throw new KeyNotFoundException($"Sport with id {id} not found");

            sport.IsActive = !sport.IsActive;
            await _sportRepository.UpdateAsync(id, sport);
            return sport;
        }
    }
}
