using ConsumerService.Options;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ConsumerService.Storage;

public sealed class MongoTelemetryRepository : ITelemetryRepository
{
    private readonly IMongoCollection<BsonDocument> _collection;

    public MongoTelemetryRepository(IOptions<MongoOptions> options)
    {
        var mongoOptions = options.Value;
        var client = new MongoClient(mongoOptions.ConnectionString);
        var database = client.GetDatabase(mongoOptions.DatabaseName);
        _collection = database.GetCollection<BsonDocument>(mongoOptions.CollectionName);
    }

    public async Task UpsertAsync(
        string messageId,
        BsonDocument telemetryEvent,
        CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("MessageId", messageId);
        telemetryEvent["ProcessedAtUtc"] = DateTime.UtcNow;

        await _collection.ReplaceOneAsync(
            filter,
            telemetryEvent,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }
}

