using Beckend.Repositories.Common;
using MongoDB.Bson;
using MongoDB.Driver;
using TeamEntity = Beckend.Models.Team;

namespace Beckend.Repositories.Teams
{
    public class TeamRepository :
        BaseRepository<TeamEntity>, ITeamRepository
    {
        public TeamRepository(IMongoDatabase database)
            : base(database, "Teams")
        {
        }

        public async Task<TeamEntity?> GetTeamByNameAsync(
            string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Team name cannot be empty",
                    nameof(name));

            return await _collection
                .Find(t => t.TeamName == name)
                .FirstOrDefaultAsync();
        }

        public async Task<List<TeamEntity>>
            GetTeamsByNameSearchAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<TeamEntity>();

            var escaped = System.Text.RegularExpressions.Regex
                .Escape(searchTerm);

            var filter = Builders<TeamEntity>.Filter.Regex(
                t => t.TeamName,
                new BsonRegularExpression(escaped, "i"));

            return await _collection
                .Find(filter)
                .ToListAsync();
        }

        public async Task<List<TeamEntity>>
            GetAllTeamsPagedAsync(int page, int pageSize)
        {
            if (page < 1)
                throw new ArgumentOutOfRangeException(nameof(page));

            if (pageSize < 1)
                throw new ArgumentOutOfRangeException(nameof(pageSize));

            return await _collection
                .Find(_ => true)
                .SortBy(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();
        }
    }
}
