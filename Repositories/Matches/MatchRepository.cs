using Beckend.Enums;
using Beckend.Models;
using MongoDB.Driver;
using Beckend.Repositories.Common;
namespace Beckend.Repositories.Matches
{
    public class MatchRepository : BaseRepository<Match>, IMatchRepository
    {

        public MatchRepository(IMongoDatabase database)
            : base(database, "Matches")
        {
            var indexKeys = Builders<Match>.IndexKeys
                .Ascending(m => m.TournamentId)
                .Ascending(m => m.StageId)
                .Ascending(m => m.RoundNumber)
                .Ascending(m => m.BracketPosition);

            var options = new CreateIndexOptions<Match>
            {
                Name = "ux_olympic_match_position",
                Unique = true,
                PartialFilterExpression =
                    Builders<Match>.Filter.And(
                        Builders<Match>.Filter.Eq(
                            m => m.IsAutomatic, true),
                        Builders<Match>.Filter.Eq(
                            m => m.StageId, "main"))
            };

            _collection.Indexes.CreateOne(
                new CreateIndexModel<Match>(
                    indexKeys, options));
        }


        public async Task<List<Match>> GetMatchesByTournamentIdAsync(string tournamentId)
        {
            if (string.IsNullOrWhiteSpace(tournamentId))
                throw new ArgumentException("Tournament ID cannot be empty", nameof(tournamentId));

            return await _collection
                .Find(m => m.TournamentId == tournamentId)
                .ToListAsync();
        }
        public async Task CreateManyAsync(List<Match> matches)
        {
            if (matches == null || matches.Count == 0)
                return;

            await _collection.InsertManyAsync(matches);
        }
        public async Task<List<Match>> GetMatchesByTeamIdAsync(string teamId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
                throw new ArgumentException("Team ID cannot be empty", nameof(teamId));

            var filter = Builders<Match>.Filter
                .Where(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId);

            return await _collection.Find(filter).ToListAsync();
        }
        public async Task<List<Match>> GetUpcomingMatchesAsync()
        {
            return await _collection
                .Find(m => m.StartTime > DateTime.UtcNow)
                .SortBy(m => m.StartTime)
                .ToListAsync();
        }
        public async Task<List<Match>> GetMatchesByDateRangeAsync(DateTime start, DateTime end)
        {
            var filter = Builders<Match>.Filter
                .Where(m => m.StartTime >= start && m.StartTime <= end);

            return await _collection.Find(filter).ToListAsync();
        }
        public async Task<Match?> GetMatchByNameAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Match name cannot be empty", nameof(name));

            return await _collection
                .Find(m => m.MatchName == name)
                .FirstOrDefaultAsync();
        }
        public async Task<List<Match>> GetMatchesByNameSearchAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Match>();
            var filter = Builders<Match>.Filter.Regex(
                m => m.MatchName,
                new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")
            );

            return await _collection.Find(filter).ToListAsync();
        }

        public async Task CreateManyAsync(IClientSessionHandle session,List<Match> matches)
        {
            if (matches == null || matches.Count == 0)
                return;

            await _collection.InsertManyAsync(
                session,
                matches);
        }

        public async Task<bool> CompleteMatchAsync(IClientSessionHandle session,Match match)
        {
            var filter = Builders<Match>.Filter.And(
                Builders<Match>.Filter.Eq(m => m.Id, match.Id),
                Builders<Match>.Filter.Ne(
                    m => m.Status, MatchStatus.Completed),
                Builders<Match>.Filter.Ne(
                    m => m.Status, MatchStatus.Cancelled)
            );

            var update = Builders<Match>.Update
                .Set(m => m.HomeScore, match.HomeScore)
                .Set(m => m.AwayScore, match.AwayScore)
                .Set(m => m.WinnerId, match.WinnerId)
                .Set(m => m.Status, MatchStatus.Completed);

            var result = await _collection.UpdateOneAsync(
                session, filter, update);

            return result.ModifiedCount == 1;
        }

        public async Task<bool> HasUnfinishedMatchesAsync( IClientSessionHandle session,string tournamentId)
        {
            var filter = Builders<Match>.Filter.And(
                Builders<Match>.Filter.Eq(
                    m => m.TournamentId, tournamentId),
                Builders<Match>.Filter.Or(
                    Builders<Match>.Filter.Ne(
                        m => m.Status, MatchStatus.Completed),
                    Builders<Match>.Filter.Eq(
                        m => m.HomeScore, null),
                    Builders<Match>.Filter.Eq(
                        m => m.AwayScore, null)
                )
            );

            return await _collection
                .Find(session, filter)
                .AnyAsync();
        }
        public async Task<List<Match>> GetSwissMatchesAsync( IClientSessionHandle session, string tournamentId)
        {
            return await _collection
                .Find(session, m => m.TournamentId == tournamentId)
                .ToListAsync();
        }

    }
}