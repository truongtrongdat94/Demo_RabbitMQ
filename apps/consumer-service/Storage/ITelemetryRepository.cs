using MongoDB.Bson;

namespace ConsumerService.Storage;

public interface ITelemetryRepository
{
    Task UpsertAsync(
        string messageId,
        BsonDocument telemetryEvent,
        CancellationToken cancellationToken);
}

