using Beckend.Enums;
using Beckend.Models;
using MongoDB.Driver;
using Beckend.Repositories.Common;

namespace Beckend.Repositories.Sports
{
    public class SportRepository : BaseRepository<Sport>, ISportRepository
    {
        public SportRepository(IMongoDatabase database) : base(database, "Sports") { }

        public async Task<Sport?> GetSportByNameAsync(SportName sportName)
        {
            return await _collection
                .Find(s => s.SportName == sportName)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Sport>> GetSportsByTypeAsync(TypeSport type)
        {
            return await _collection
                .Find(s => s.Type == type)
                .ToListAsync();
        }
        public async Task<List<Sport>> GetActiveSportsAsync()
        {
            return await _collection
                .Find(s => s.IsActive == true)
                .ToListAsync();
        }
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