using MongoDB.Bson;
using MongoDB.Driver;

namespace Beckend.Repositories
{
    public abstract class BaseRepository<T> where T : class
    {
        protected readonly IMongoCollection<T> _collection;

        protected BaseRepository(
            IMongoDatabase database,
            string collectionName)
        {
            _collection = database.GetCollection<T>(collectionName);
        }

        public async Task<List<T>> GetAllAsync()
        {
            return await _collection
                .Find(_ => true)
                .ToListAsync();
        }

        public async Task<T?> GetByIdAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return null;

            return await _collection
                .Find(Builders<T>.Filter.Eq("_id", objectId))
                .FirstOrDefaultAsync();
        }

        public async Task CreateAsync(T entity)
        {
            await _collection.InsertOneAsync(entity);
        }

        public async Task UpdateAsync(string id, T entity)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format");

            var result = await _collection.ReplaceOneAsync(
                Builders<T>.Filter.Eq("_id", objectId),
                entity);

            if (result.MatchedCount == 0)
                throw new KeyNotFoundException("Document not found");
        }

        public async Task DeleteAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid ID format");

            await _collection.DeleteOneAsync(
                Builders<T>.Filter.Eq("_id", objectId));
        }
    }
}