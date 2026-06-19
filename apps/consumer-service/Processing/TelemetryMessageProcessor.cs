using System.Text.Json;
using ConsumerService.Storage;
using MongoDB.Bson;

namespace ConsumerService.Processing;

public sealed class TelemetryMessageProcessor
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow
    };

    private readonly IRepositoryManager _repositories;
    private readonly ILogger<TelemetryMessageProcessor> _logger;

    public TelemetryMessageProcessor(
        IRepositoryManager repositories,
        ILogger<TelemetryMessageProcessor> logger)
    {
        _repositories = repositories;
        _logger = logger;
    }

    public async Task ProcessAsync(
        ReadOnlyMemory<byte> payload,
        string queueName,
        string routingKey,
        CancellationToken cancellationToken)
    {
        using var document = ParsePayload(payload);
        ValidateRequiredFields(document.RootElement);

        var testCase = GetRequiredString(document.RootElement, "Meta", "TestCase");
        var messageId = GetRequiredString(document.RootElement, "MessageId");

        switch (testCase)
        {
            case "VALID_SAVE_DB":
            case "STEAM_VALID":
            case "GAS_VALID":
                await SaveAsync(payload, messageId, cancellationToken);
                _logger.LogInformation(
                    "Persisted telemetry message {MessageId} from queue {QueueName} with routing key {RoutingKey}",
                    messageId,
                    queueName,
                    routingKey);
                return;

            case "VALID_NO_SAVE":
                _logger.LogInformation(
                    "Validated telemetry message {MessageId} from queue {QueueName}; skipping persistence by test case",
                    messageId,
                    queueName);
                return;

            case "VALID_FORCE_DB_ERROR":
                throw new TransientMessageException(
                    $"Simulated transient database failure for message {messageId}");

            case "INVALID_SCHEMA":
                throw new PermanentMessageException(
                    $"Invalid schema test case received for message {messageId}");

            default:
                throw new PermanentMessageException(
                    $"Unsupported test case '{testCase}' for message {messageId}");
        }
    }

    private async Task SaveAsync(
        ReadOnlyMemory<byte> payload,
        string messageId,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = System.Text.Encoding.UTF8.GetString(payload.ToArray());
            var bsonDocument = BsonDocument.Parse(json);
            await _repositories.Telemetry.UpsertAsync(messageId, bsonDocument, cancellationToken);
        }
        catch (MongoDB.Driver.MongoException exception)
        {
            throw new TransientMessageException(
                $"MongoDB write failed for message {messageId}",
                exception);
        }
    }

    private static JsonDocument ParsePayload(ReadOnlyMemory<byte> payload)
    {
        try
        {
            return JsonDocument.Parse(payload, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new PermanentMessageException($"Payload is not valid JSON: {exception.Message}");
        }
    }

    private static void ValidateRequiredFields(JsonElement root)
    {
        _ = GetRequiredString(root, "MessageId");
        _ = GetRequiredString(root, "CorrelationId");
        _ = GetRequiredString(root, "Source");
        _ = GetRequiredString(root, "SchemaVersion");
        _ = GetRequiredString(root, "MessageType");
        _ = GetRequiredString(root, "ID_Gateway");

        if (!root.TryGetProperty("Timestamp_Gateway", out var timestampGateway)
            || timestampGateway.ValueKind is not JsonValueKind.Number)
        {
            throw new PermanentMessageException("Timestamp_Gateway is required and must be a number");
        }

        if (!root.TryGetProperty("Data_Gateway", out var dataGateway)
            || dataGateway.ValueKind is not JsonValueKind.Array
            || dataGateway.GetArrayLength() == 0)
        {
            throw new PermanentMessageException("Data_Gateway must contain at least one item");
        }

        _ = GetRequiredString(root, "Meta", "TestCase");
    }

    private static string GetRequiredString(JsonElement root, params string[] path)
    {
        var current = root;

        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                throw new PermanentMessageException($"{string.Join(".", path)} is missing");
            }
        }

        if (current.ValueKind is not JsonValueKind.String)
        {
            throw new PermanentMessageException($"{string.Join(".", path)} must be a string");
        }

        var value = current.GetString();

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new PermanentMessageException($"{string.Join(".", path)} is empty");
        }

        return value;
    }
}
