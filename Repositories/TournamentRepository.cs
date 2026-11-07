using Beckend.Enums;
using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Repositories
{
    public class TournamentRepository : BaseRepository<Tournament>
    {
        public TournamentRepository(IMongoDatabase database)
    : base(database, "Tournaments") { }
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

            // Використовуємо Regex для часткового пошуку (case-insensitive)
            var filter = Builders<Tournament>.Filter.Regex(
                t => t.TournamentName,
                new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")
            );

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
            var activeStatuses = new[]
            {
        TournamentStatus.Registration,
        TournamentStatus.Ready,
        TournamentStatus.InProgress
    };

            var filter = Builders<Tournament>.Filter.In(
                t => t.Status,
                activeStatuses);

            return await _collection
                .Find(filter)
                .ToListAsync();
        }
    }
}
