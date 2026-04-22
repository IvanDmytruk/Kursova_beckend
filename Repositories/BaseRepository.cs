using MongoDB.Driver;
using Microsoft.Extensions.Configuration;

namespace Beckend.Repositories
{
    public abstract class BaseRepository<T> where T : class
    {
        protected readonly IMongoCollection<T> _collection;

        protected BaseRepository(IConfiguration config, string collectionName)
        {
            var connectionString = config.GetConnectionString("MongoDB");

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("MongoDB connection string is not configured");
            }

            var client = new MongoClient(connectionString);
            var database = client.GetDatabase("CreateadTournament");
            _collection = database.GetCollection<T>(collectionName);
        }

        public async Task<List<T>> GetAllAsync() =>
            await _collection.Find(_ => true).ToListAsync();

        public async Task<T?> GetByIdAsync(string id) =>
            await _collection.Find(Builders<T>.Filter.Eq("_id", id)).FirstOrDefaultAsync();

        public async Task CreateAsync(T entity) =>
            await _collection.InsertOneAsync(entity);

        public async Task UpdateAsync(string id, T entity) =>
            await _collection.ReplaceOneAsync(Builders<T>.Filter.Eq("_id", id), entity);

        public async Task DeleteAsync(string id) =>
            await _collection.DeleteOneAsync(Builders<T>.Filter.Eq("_id", id));
    }
}
