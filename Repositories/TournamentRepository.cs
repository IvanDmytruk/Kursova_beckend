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

        public async Task<bool> CompleteOlympicTournamentAsync(string tournamentId)
        {
            var filter = Builders<Tournament>.Filter.And(
                Builders<Tournament>.Filter.Eq(
                    t => t.Id, tournamentId),
                Builders<Tournament>.Filter.Eq(
                    t => t.TournamentType,
                    TournamentType.OlympicSystem),
                Builders<Tournament>.Filter.Eq(
                    t => t.Status,
                    TournamentStatus.InProgress)
            );

            var update = Builders<Tournament>.Update
                .Set(t => t.Status, TournamentStatus.Completed);

            var result = await _collection.UpdateOneAsync(
                filter, update);

            return result.ModifiedCount == 1;
        }

        public async Task<bool> CompleteOlympicTournamentAsync(IClientSessionHandle session,string tournamentId)
        {
            var filter = Builders<Tournament>.Filter.And(
                Builders<Tournament>.Filter.Eq(
                    t => t.Id, tournamentId),
                Builders<Tournament>.Filter.Eq(
                    t => t.TournamentType,
                    TournamentType.OlympicSystem),
                Builders<Tournament>.Filter.Eq(
                    t => t.Status,
                    TournamentStatus.InProgress));

            var update = Builders<Tournament>.Update
                .Set(t => t.Status, TournamentStatus.Completed);

            var result = await _collection.UpdateOneAsync(
                session, filter, update);

            return result.ModifiedCount == 1;
        }

        public async Task<bool> StartOlympicTournamentAsync(
            IClientSessionHandle session,
            string tournamentId)
        {
            var filter = Builders<Tournament>.Filter.And(
                Builders<Tournament>.Filter.Eq(
                    t => t.Id, tournamentId),
                Builders<Tournament>.Filter.Eq(
                    t => t.TournamentType,
                    TournamentType.OlympicSystem),
                Builders<Tournament>.Filter.Eq(
                    t => t.Status,
                    TournamentStatus.Ready)
            );

            var update = Builders<Tournament>.Update
                .Set(t => t.Status, TournamentStatus.InProgress);

            var result = await _collection.UpdateOneAsync(
                session, filter, update);

            return result.ModifiedCount == 1;
        }

        public async Task<bool> StartTournamentAsync(
            IClientSessionHandle session,
            string tournamentId,
            TournamentType tournamentType)
        {
            var filter = Builders<Tournament>.Filter.And(
                Builders<Tournament>.Filter.Eq(
                    t => t.Id, tournamentId),
                Builders<Tournament>.Filter.Eq(
                    t => t.TournamentType, tournamentType),
                Builders<Tournament>.Filter.Eq(
                    t => t.Status, TournamentStatus.Ready)
            );

            var update = Builders<Tournament>.Update
                .Set(t => t.Status, TournamentStatus.InProgress);

            var result = await _collection.UpdateOneAsync(
                session, filter, update);

            return result.ModifiedCount == 1;
        }

        public async Task<bool> CompleteRoundRobinAsync(
            IClientSessionHandle session,
            string tournamentId)
        {
            var filter = Builders<Tournament>.Filter.And(
                Builders<Tournament>.Filter.Eq(
                    t => t.Id, tournamentId),
                Builders<Tournament>.Filter.Eq(
                    t => t.TournamentType,
                    TournamentType.RoundRobin),
                Builders<Tournament>.Filter.Eq(
                    t => t.Status,
                    TournamentStatus.InProgress)
            );

            var update = Builders<Tournament>.Update
                .Set(t => t.Status, TournamentStatus.Completed);

            var result = await _collection.UpdateOneAsync(
                session, filter, update);

            return result.ModifiedCount == 1;
        }

        public async Task<bool> AdvanceSwissRoundAsync(
            IClientSessionHandle session,
            string tournamentId,
            int expectedRound)
        {
            var filter = Builders<Tournament>.Filter.And(
                Builders<Tournament>.Filter.Eq(t => t.Id, tournamentId),
                Builders<Tournament>.Filter.Eq(
                    t => t.TournamentType, TournamentType.SwissSystem),
                Builders<Tournament>.Filter.Eq(
                    t => t.Status, TournamentStatus.InProgress),
                Builders<Tournament>.Filter.Eq(
                    t => t.SwissLastGeneratedRound, expectedRound)
            );

            var update = Builders<Tournament>.Update
                .Set(t => t.SwissLastGeneratedRound, expectedRound + 1);

            var result = await _collection.UpdateOneAsync(
                session, filter, update);

            return result.ModifiedCount == 1;
        }

        public async Task<bool> StartSwissTournamentAsync(
            IClientSessionHandle session,
            string tournamentId)
        {
            var filter = Builders<Tournament>.Filter.And(
                Builders<Tournament>.Filter.Eq(t => t.Id, tournamentId),
                Builders<Tournament>.Filter.Eq(
                    t => t.TournamentType, TournamentType.SwissSystem),
                Builders<Tournament>.Filter.Eq(
                    t => t.Status, TournamentStatus.Ready),
                Builders<Tournament>.Filter.Eq(
                    t => t.SwissLastGeneratedRound, 0)
            );

            var update = Builders<Tournament>.Update
                .Set(t => t.Status, TournamentStatus.InProgress)
                .Set(t => t.SwissLastGeneratedRound, 1);

            var result = await _collection.UpdateOneAsync(
                session, filter, update);

            return result.ModifiedCount == 1;
        }
        public async Task<Tournament?> GetSwissTournamentAsync(
            IClientSessionHandle session,
            string tournamentId)
        {
            return await _collection
                .Find(session, t => t.Id == tournamentId)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> CompleteFixedRoundsSwissAsync(
            IClientSessionHandle session,
            string tournamentId,
            int expectedRound)
        {
            var filter = Builders<Tournament>.Filter.And(
                Builders<Tournament>.Filter.Eq(
                    t => t.Id, tournamentId),
                Builders<Tournament>.Filter.Eq(
                    t => t.TournamentType, TournamentType.SwissSystem),
                Builders<Tournament>.Filter.Eq(
                    t => t.Status, TournamentStatus.InProgress),
                Builders<Tournament>.Filter.Eq(
                    t => t.SwissLastGeneratedRound, expectedRound),
                Builders<Tournament>.Filter.Eq(
                    t => t.Settings.SwissMode, SwissMode.FixedRounds),
                Builders<Tournament>.Filter.Eq(
                    t => t.Settings.RoundsCount, expectedRound)
            );

            var update = Builders<Tournament>.Update
                .Set(t => t.Status, TournamentStatus.Completed);

            var result = await _collection.UpdateOneAsync(
                session, filter, update);

            return result.ModifiedCount == 1;
        }
        public async Task<bool> CompleteThresholdSwissAsync(
            IClientSessionHandle session,
            string tournamentId)
        {
            var filter = Builders<Tournament>.Filter.And(
                Builders<Tournament>.Filter.Eq(
                    t => t.Id, tournamentId),
                Builders<Tournament>.Filter.Eq(
                    t => t.TournamentType, TournamentType.SwissSystem),
                Builders<Tournament>.Filter.Eq(
                    t => t.Status, TournamentStatus.InProgress),
                Builders<Tournament>.Filter.Eq(
                    t => t.Settings.SwissMode,
                    SwissMode.WinLossThreshold)
            );

            var update = Builders<Tournament>.Update
                .Set(t => t.Status, TournamentStatus.Completed);

            var result = await _collection.UpdateOneAsync(
                session, filter, update);

            return result.ModifiedCount == 1;
        }

    }
}
