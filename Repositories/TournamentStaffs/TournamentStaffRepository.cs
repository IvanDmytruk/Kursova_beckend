using Beckend.Enums;
using Beckend.Models;
using MongoDB.Driver;

namespace Beckend.Repositories.TournamentStaffs;

public class TournamentStaffRepository: ITournamentStaffRepository
{
    private readonly IMongoCollection<Models.TournamentStaff> _collection;

    public TournamentStaffRepository(IMongoDatabase database)
    {
        _collection = database
            .GetCollection<Models.TournamentStaff>("TournamentStaff");
    }
    public async Task EnsureIndexesAsync()
    {
        var keys = Builders<Models.TournamentStaff>.IndexKeys
            .Ascending(s => s.TournamentId)
            .Ascending(s => s.UserId);

        var options = new CreateIndexOptions<Models.TournamentStaff>
        {
            Unique = true,
            PartialFilterExpression =
                Builders<Models.TournamentStaff>.Filter.Eq(
                    s => s.IsActive, true)
        };

        await _collection.Indexes.CreateOneAsync(
            new CreateIndexModel<Models.TournamentStaff>(
                keys, options));
    }
    public async Task<Models.TournamentStaff?> GetActiveAsync(
        string tournamentId,
        string userId)
    {
        return await _collection.Find(s =>
            s.TournamentId == tournamentId &&
            s.UserId == userId &&
            s.IsActive).FirstOrDefaultAsync();
    }

    public async Task<List<Models.TournamentStaff>> GetByTournamentAsync(
        string tournamentId)
    {
        return await _collection.Find(s =>
            s.TournamentId == tournamentId &&
            s.IsActive).ToListAsync();
    }

    public async Task<Models.TournamentStaff> AssignAsync(
        Models.TournamentStaff staff)
    {
        await _collection.InsertOneAsync(staff);
        return staff;
    }

    public async Task<bool> UpdatePermissionsAsync(
        string tournamentId,
        string userId,
        TournamentPermission permissions)
    {
        var filter = Builders<Models.TournamentStaff>.Filter.And(
            Builders<Models.TournamentStaff>.Filter.Eq(
                s => s.TournamentId, tournamentId),
            Builders<Models.TournamentStaff>.Filter.Eq(
                s => s.UserId, userId),
            Builders<Models.TournamentStaff>.Filter.Eq(
                s => s.IsActive, true));

        var update = Builders<Models.TournamentStaff>.Update
            .Set(s => s.Permissions, permissions);

        var result = await _collection.UpdateOneAsync(filter, update);

        return result.MatchedCount == 1;
    }

    public async Task<bool> RevokeAsync(
        string tournamentId,
        string userId)
    {
        var filter = Builders<Models.TournamentStaff>.Filter.And(
            Builders<Models.TournamentStaff>.Filter.Eq(
                s => s.TournamentId, tournamentId),
            Builders<Models.TournamentStaff>.Filter.Eq(
                s => s.UserId, userId),
            Builders<Models.TournamentStaff>.Filter.Eq(
                s => s.IsActive, true));

        var update = Builders<Models.TournamentStaff>.Update
            .Set(s => s.IsActive, false)
            .Set(s => s.RevokedAt, DateTime.UtcNow);

        var result = await _collection.UpdateOneAsync(filter, update);

        return result.ModifiedCount == 1;
    }
}
