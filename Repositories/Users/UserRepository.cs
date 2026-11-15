
using Beckend.Enums;
using Beckend.Models;
using Beckend.Repositories.Common;
using MongoDB.Driver;

namespace Beckend.Repositories.Users;

public class UserRepository : BaseRepository<User>, IUserRepository
{
    private readonly IMongoCollection<TeamMember> _teamMembers;

    public UserRepository(IMongoDatabase database)
        : base(database, "Users")
    {
        _teamMembers = database
            .GetCollection<TeamMember>("TeamMembers");
    }

    private async Task<List<User>> GetUsersByTeamRolesAsync(
        params TeamRole[] roles)
    {
        var memberships = await _teamMembers
            .Find(m => m.IsActive && roles.Contains(m.Role))
            .ToListAsync();

        var userIds = memberships
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
            return new List<User>();

        var filter = Builders<User>.Filter.In(
            u => u.Id, userIds);

        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<List<User>> SearchPlayersAndCoachesAsync(
        string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<User>();

        var memberships = await _teamMembers
            .Find(m => m.IsActive &&
                (m.Role == TeamRole.Player ||
                 m.Role == TeamRole.Coach))
            .ToListAsync();

        var userIds = memberships
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
            return new List<User>();

        var escapedSearchTerm =
            System.Text.RegularExpressions.Regex.Escape(
                searchTerm.Trim());

        var nameFilter = Builders<User>.Filter.Regex(
            u => u.Name,
            new MongoDB.Bson.BsonRegularExpression(
                escapedSearchTerm, "i"));

        var surnameFilter = Builders<User>.Filter.Regex(
            u => u.Surname,
            new MongoDB.Bson.BsonRegularExpression(
                escapedSearchTerm, "i"));

        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.In(u => u.Id, userIds),
            Builders<User>.Filter.Or(
                nameFilter,
                surnameFilter));

        return await _collection
            .Find(filter)
            .ToListAsync();
    }

    public Task<List<User>> GetAllPlayersAsync()
    {
        return GetUsersByTeamRolesAsync(TeamRole.Player);
    }

    public Task<List<User>> GetAllCoachesAsync()
    {
        return GetUsersByTeamRolesAsync(TeamRole.Coach);
    }

    public async Task<List<User>> GetPlayersAndCoachesByAgeRangeAsync(
        int minAge,
        int maxAge)
    {
        if (minAge < 0 || maxAge < minAge)
            throw new ArgumentException("Invalid age range.");

        var memberships = await _teamMembers
            .Find(m => m.IsActive &&
                (m.Role == TeamRole.Player ||
                 m.Role == TeamRole.Coach))
            .ToListAsync();

        var userIds = memberships
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
            return new List<User>();

        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.In(u => u.Id, userIds),
            Builders<User>.Filter.Gte(u => u.Age, minAge),
            Builders<User>.Filter.Lte(u => u.Age, maxAge));

        return await _collection
            .Find(filter)
            .ToListAsync();
    }

    public async Task<List<User>> GetPlayersByTeamIdAsync(
        string teamId)
    {
        if (string.IsNullOrWhiteSpace(teamId))
            return new List<User>();

        var memberships = await _teamMembers
            .Find(m =>
                m.TeamId == teamId &&
                m.Role == TeamRole.Player &&
                m.IsActive)
            .ToListAsync();

        var userIds = memberships
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
            return new List<User>();

        var filter = Builders<User>.Filter.In(
            u => u.Id, userIds);

        return await _collection
            .Find(filter)
            .ToListAsync();
    }
    public async Task<List<User>> SearchPlayersAsync(string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<User>();

        var memberships = await _teamMembers
            .Find(m =>
                m.IsActive &&
                m.Role == TeamRole.Player)
            .ToListAsync();

        var userIds = memberships
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
            return new List<User>();

        var escapedSearchTerm =
            System.Text.RegularExpressions.Regex.Escape(
                searchTerm.Trim());

        var nameFilter = Builders<User>.Filter.Regex(
            u => u.Name,
            new MongoDB.Bson.BsonRegularExpression(
                escapedSearchTerm, "i"));

        var surnameFilter = Builders<User>.Filter.Regex(
            u => u.Surname,
            new MongoDB.Bson.BsonRegularExpression(
                escapedSearchTerm, "i"));

        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.In(u => u.Id, userIds),
            Builders<User>.Filter.Or(
                nameFilter,
                surnameFilter));

        return await _collection.Find(filter).ToListAsync();
    }
    public async Task<bool> HasTeamRoleAsync(string userId, params TeamRole[] roles)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            roles == null ||
            roles.Length == 0)
        {
            return false;
        }

        return await _teamMembers
            .Find(m =>
                m.UserId == userId &&
                m.IsActive &&
                roles.Contains(m.Role))
            .AnyAsync();
    }
}
