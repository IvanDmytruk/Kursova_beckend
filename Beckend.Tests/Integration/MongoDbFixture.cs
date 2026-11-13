using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Beckend.Tests.Integration;

public sealed class MongoDbFixture : IAsyncLifetime
{
    private readonly IMongoClient _client;

    public IMongoDatabase Database { get; }

    public MongoDbFixture()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "MONGODB_TEST_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "MONGODB_TEST_CONNECTION_STRING is not configured.");
        }

        _client = new MongoClient(connectionString);

        string databaseName = $"Test_{Guid.NewGuid():N}";

        Database = _client.GetDatabase(databaseName);
    }

    public async Task InitializeAsync()
    {
        await Database.RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1));
    }

    public async Task DisposeAsync()
    {
        await _client.DropDatabaseAsync(
            Database.DatabaseNamespace.DatabaseName);
    }
}
