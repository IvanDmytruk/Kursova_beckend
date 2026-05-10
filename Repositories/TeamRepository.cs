using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Repositories
{
    public class TeamRepository : BaseRepository<Team>
    {
        public TeamRepository(IConfiguration config) : base(config, "Teams") { }

        // Пошук за назвою команди
        public async Task<Team?> GetTeamByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Team name cannot be empty", nameof(name));

            return await _collection
                .Find(t => t.TeamName == name)
                .FirstOrDefaultAsync();
        }

        // Пошук за частиною назви 
        public async Task<List<Team>> GetTeamsByNameSearchAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Team>();

            // Використовуємо Regex для часткового пошуку
            var filter = Builders<Team>.Filter.Regex(
                t => t.TeamName,
                new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")
            );

            return await _collection.Find(filter).ToListAsync();
        }

        // Отримати всі команди з пагінацією
        public async Task<List<Team>> GetAllTeamsPagedAsync(int page, int pageSize)
        {
            return await _collection
                .Find(_ => true)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
    }
}