using Beckend.Repositories;
using Beckend.Models;
using Beckend.Enums;
using System.Diagnostics.Metrics;

namespace Beckend.Services
{
    public class UserService
    {
        private readonly UserRepository _userRepository;
        public UserService (UserRepository userRepository)
        {
            _userRepository = userRepository; 
        }
        //Пошук гравців та тренерів за ім'ям
        public async Task<List<User>> SearchPlayersAndCoachesAsync(string? name = null, string? surname = null)
        {
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(surname))
                throw new ArgumentException("At least name or surname must be provided");

            var users = await _userRepository.SearchPlayersAndCoachesAsync(name, surname);

            if (users.Count == 0)
                throw new KeyNotFoundException("No players or coaches found");

            return users;
        }

        // Отримати всіх гравців
        public async Task<List<User>> GetAllPlayersAsync()
        {
            var players = await _userRepository.GetAllPlayersAsync();

            if (players == null || players.Count == 0)
                throw new KeyNotFoundException("No players found");

            return players;
        }

        // Отримати всіх тренерів
        public async Task<List<User>> GetAllCoachesAsync()
        {
            var coaches = await _userRepository.GetAllCoachesAsync();

            if (coaches == null || coaches.Count == 0)
                throw new KeyNotFoundException("No coaches found");

            return coaches;
        }
        // Отримати гравців та тренерів за віком
        public async Task<List<User>> GetPlayersAndCoachesByAgeRangeAsync(int minAge, int maxAge)
        {
            if (minAge < 0 || maxAge < minAge)
                throw new ArgumentException("Invalid age range");

            var users = await _userRepository.GetPlayersAndCoachesByAgeRangeAsync(minAge, maxAge);

            if (users == null || users.Count == 0)
                throw new KeyNotFoundException($"No players or coaches found in age range {minAge}-{maxAge}");

            return users;
        }

        // Перевірка чи користувач гравець або тренер
        public bool IsPlayerOrCoach(User user)
        {
            return user != null && (user.Role == UserRole.Player || user.Role == UserRole.Coach);
        }

        // Отримати гравця або тренера за ID (з перевіркою)
        public async Task<User?> GetPlayerOrCoachByIdAsync(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);

            if (user == null)
                throw new KeyNotFoundException($"User with id {id} not found");

            if (!IsPlayerOrCoach(user))
                throw new UnauthorizedAccessException($"User with id {id} is not a player or coach");

            return user;
        }

        // Стандартні методи (для адмінів)
        public async Task<List<User>> GetAllAsync() =>
            await _userRepository.GetAllAsync();

        public async Task<User?> GetByIdAsync(string id) =>
            await _userRepository.GetByIdAsync(id);

        public async Task<User> CreateAsync(User user)
        {
            // Валідація
            if (string.IsNullOrWhiteSpace(user.Name))
                throw new ArgumentException("User name is required");

            if (string.IsNullOrWhiteSpace(user.Surname))
                throw new ArgumentException("User surname is required");

            if (user.Age < 0 || user.Age > 120)
                throw new ArgumentException("Invalid age");

            await _userRepository.CreateAsync(user);
            return user;
        }

        public async Task<User> UpdateAsync(string id, User user)
        {
            var existing = await _userRepository.GetByIdAsync(id);
            if (existing == null)
                throw new KeyNotFoundException($"User with id {id} not found");

            user.Id = id;
            await _userRepository.UpdateAsync(id, user);
            return user;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var existing = await _userRepository.GetByIdAsync(id);
            if (existing == null)
                return false;

            await _userRepository.DeleteAsync(id);
            return true;
        }
    }
}
