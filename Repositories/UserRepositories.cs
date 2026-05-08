using Beckend.Enums;
using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Repositories
{
    public class UserRepository: BaseRepository<User>
    {
        public UserRepository(IConfiguration config) : base(config, "Users") { }
        public async Task<List<User>> SearchPlayersAndCoachesAsync(string? name, string? surname)
        {
            var roleFilter = Builders<User>.Filter.Or(
                Builders<User>.Filter.Eq(u => u.Role, UserRole.Player),
                Builders<User>.Filter.Eq(u => u.Role, UserRole.Coach)
            );

            var filters = new List<FilterDefinition<User>> { roleFilter };

            if (!string.IsNullOrWhiteSpace(name))
            {
                filters.Add(
                    Builders<User>.Filter.Regex(
                        u => u.Name,
                        new MongoDB.Bson.BsonRegularExpression(name, "i")
                    )
                );
            }

            if (!string.IsNullOrWhiteSpace(surname))
            {
                filters.Add(
                    Builders<User>.Filter.Regex(
                        u => u.Surname,
                        new MongoDB.Bson.BsonRegularExpression(surname, "i")
                    )
                );
            }

            var filter = Builders<User>.Filter.And(filters);

            return await _collection.Find(filter).ToListAsync();
        }

        //Отримати всіх гравців
        public async Task<List<User>> GetAllPlayersAsync()
        {
            return await _collection
                .Find(u => u.Role == UserRole.Player)
                .ToListAsync();
        }

        //Отримати всіх тренерів
        public async Task<List<User>> GetAllCoachesAsync()
        {
            return await _collection
                .Find(u => u.Role == UserRole.Coach)
                .ToListAsync();
        }
        //Пошук за віком
        public async Task<List<User>> GetPlayersAndCoachesByAgeRangeAsync(int minAge, int maxAge)
        {
            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Or(
                    Builders<User>.Filter.Eq(u => u.Role, UserRole.Player),
                    Builders<User>.Filter.Eq(u => u.Role, UserRole.Coach)
                ),
                Builders<User>.Filter.Gte(u => u.Age, minAge),
                Builders<User>.Filter.Lte(u => u.Age, maxAge)
            );

            return await _collection.Find(filter).ToListAsync();
        }
        public async Task<List<User>> GetPlayersByTeamIdAsync(string teamId)
        {
            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(u => u.Role, UserRole.Player),
                Builders<User>.Filter.Eq("PlayerProfile.TeamId", teamId)
            );

            return await _collection.Find(filter).ToListAsync();
        }
    }
}