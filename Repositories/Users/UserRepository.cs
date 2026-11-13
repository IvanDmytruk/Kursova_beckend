using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Common;
using MongoDB.Driver;

namespace Beckend.Repositories.Users;

public class UserRepository: BaseRepository<User>, IUserRepository
{
        public UserRepository(IMongoDatabase database) : base(database, "Users") { }
        public async Task<List<User>> SearchPlayersAndCoachesAsync(string? searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<User>();

            var roleFilter = Builders<User>.Filter.Or(
                Builders<User>.Filter.Eq(u => u.Role, UserRole.Player),
                Builders<User>.Filter.Eq(u => u.Role, UserRole.Coach)
            );

        var escapedSearchTerm = System.Text.RegularExpressions.Regex.Escape(searchTerm);

        var nameFilter = Builders<User>.Filter.Regex(
            u => u.Name,
            new MongoDB.Bson.BsonRegularExpression(
                escapedSearchTerm, "i"));

        var surnameFilter = Builders<User>.Filter.Regex(
            u => u.Surname,
            new MongoDB.Bson.BsonRegularExpression(
                escapedSearchTerm, "i"));

        var searchFilter = Builders<User>.Filter.Or(nameFilter, surnameFilter);

            var filter = Builders<User>.Filter.And(roleFilter, searchFilter);

            return await _collection.Find(filter).ToListAsync();
        }
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
