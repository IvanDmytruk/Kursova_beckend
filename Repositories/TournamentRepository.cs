using Beckend.Enums;
using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Repositories
{
    public class TournamentRepository : BaseRepository<Tournament>
    {
        public TournamentRepository(IConfiguration config) : base(config, "Tournaments") { }
        // метод для пошуку за назвою
        public async Task<Tournament?> GetTournamentByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tournament name cannot be empty", nameof(name));

            return await _collection
                .Find(t => t.TournamentName == name)
                .FirstOrDefaultAsync();
        }

        // Пошук за частиною назви 
        public async Task<List<Tournament>> GetTournamentsByNameSearchAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Tournament>();

            var filter = Builders<Tournament>.Filter
                .Where(t => t.TournamentName.Contains(searchTerm));

            return await _collection.Find(filter).ToListAsync();
        }

        //  Пошук за типом турніру
        public async Task<List<Tournament>> GetTournamentsByTypeAsync(TournamentType type)
        {
            return await _collection
                .Find(t => t.TournamentType == type)
                .ToListAsync();
        }

        //  Пошук активних турнірів
        public async Task<List<Tournament>> GetActiveTournamentsAsync()
        {
            return await _collection
                .Find(t => t.EndDate == null || t.EndDate > DateTime.UtcNow)
                .ToListAsync();
        }
    }
}
