using Beckend.Enums;
using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Repositories
{
    public class SportRepository : BaseRepository<Sport>
    {
        public SportRepository(IConfiguration config) : base(config, "Sports") { }

        // Пошук за назвою виду спорту
        public async Task<Sport?> GetSportByNameAsync(SportName sportName)
        {
            return await _collection
                .Find(s => s.SportName == sportName)
                .FirstOrDefaultAsync();
        }

        // Пошук за типом спорту
        public async Task<List<Sport>> GetSportsByTypeAsync(TypeSport type)
        {
            return await _collection
                .Find(s => s.Type == type)
                .ToListAsync();
        }

        // Отримати всі активні види спорту
        public async Task<List<Sport>> GetActiveSportsAsync()
        {
            return await _collection
                .Find(s => s.IsActive == true)
                .ToListAsync();
        }

        // Пошук за частиною опису
        public async Task<List<Sport>> GetSportsByDescriptionSearchAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Sport>();

            var filter = Builders<Sport>.Filter
                .Where(s => s.SportDescription.Contains(searchTerm));

            return await _collection.Find(filter).ToListAsync();
        }
    }
}