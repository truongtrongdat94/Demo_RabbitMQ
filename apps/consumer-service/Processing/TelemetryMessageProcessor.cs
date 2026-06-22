using System.Text;
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
        var json = Encoding.UTF8.GetString(payload.ToArray());
        using var document = ParsePayload(payload);
        var envelope = ValidateAndReadEnvelope(document.RootElement);

        switch (envelope.TestCase)
        {
            case "VALID_SAVE_DB":
            case "STEAM_VALID":
            case "GAS_VALID":
                ValidateSimulationFlags(
                    envelope,
                    expectedNoSave: false,
                    expectedForceDbError: false);

                await SaveAsync(json, envelope.MessageId, cancellationToken);
                _logger.LogInformation(
                    "Persisted telemetry message {MessageId} from queue {QueueName} with routing key {RoutingKey}",
                    envelope.MessageId,
                    queueName,
                    routingKey);
                return;

            case "VALID_NO_SAVE":
                ValidateSimulationFlags(
                    envelope,
                    expectedNoSave: true,
                    expectedForceDbError: false);

                _logger.LogInformation(
                    "Validated telemetry message {MessageId} from queue {QueueName}; Simulate.NoSave=true; payload: {Payload}",
                    envelope.MessageId,
                    queueName,
                    json);
                return;

            case "VALID_FORCE_DB_ERROR":
                ValidateSimulationFlags(
                    envelope,
                    expectedNoSave: false,
                    expectedForceDbError: true);

                throw new TransientMessageException(
                    $"Simulated transient database failure for message {envelope.MessageId}");

            case "INVALID_SCHEMA":
                throw new PermanentMessageException(
                    $"INVALID_SCHEMA test case did not fail schema validation for message {envelope.MessageId}");

            default:
                throw new PermanentMessageException(
                    $"Unsupported test case '{envelope.TestCase}' for message {envelope.MessageId}");
        }
    }

    private async Task SaveAsync(
        string json,
        string messageId,
        CancellationToken cancellationToken)
    {
        try
        {
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

    private static TelemetryEnvelope ValidateAndReadEnvelope(JsonElement root)
    {
        EnsureObject(root, "payload");

        var messageId = GetRequiredString(root, "MessageId");
        _ = GetRequiredString(root, "CorrelationId");
        _ = GetRequiredString(root, "Source");
        _ = GetRequiredString(root, "SchemaVersion");
        _ = GetRequiredString(root, "MessageType");
        _ = GetRequiredString(root, "ID_Gateway");
        _ = GetRequiredNumber(root, "Timestamp_Gateway");

        ValidateDataGateway(root);

        var testCase = GetRequiredString(root, "Meta", "TestCase");
        var noSave = GetRequiredBoolean(root, "Simulate", "NoSave");
        var forceDbError = GetRequiredBoolean(root, "Simulate", "ForceDbError");

        return new TelemetryEnvelope(
            messageId,
            testCase,
            noSave,
            forceDbError);
    }

    private static void ValidateDataGateway(JsonElement root)
    {
        var dataGateway = GetRequiredArray(root, "Data_Gateway");

        if (dataGateway.GetArrayLength() == 0)
        {
            throw new PermanentMessageException("Data_Gateway must contain at least one item");
        }

        var gatewayIndex = 0;
        foreach (var gateway in dataGateway.EnumerateArray())
        {
            var gatewayPath = $"Data_Gateway[{gatewayIndex}]";
            EnsureObject(gateway, gatewayPath);

            _ = GetRequiredStringProperty(gateway, gatewayPath, "Topic");
            var devices = GetRequiredArrayProperty(gateway, gatewayPath, "Data_Devices");

            if (devices.GetArrayLength() == 0)
            {
                throw new PermanentMessageException($"{gatewayPath}.Data_Devices must contain at least one item");
            }

            var deviceIndex = 0;
            foreach (var device in devices.EnumerateArray())
            {
                ValidateDevice(device, $"{gatewayPath}.Data_Devices[{deviceIndex}]");
                deviceIndex++;
            }

            gatewayIndex++;
        }
    }

    private static void ValidateDevice(JsonElement device, string devicePath)
    {
        EnsureObject(device, devicePath);

        _ = GetRequiredStringProperty(device, devicePath, "ID_Device");
        _ = GetRequiredStringProperty(device, devicePath, "Type_Device");
        _ = GetRequiredNumberProperty(device, devicePath, "Timestamp_Device");

        var reading = GetRequiredObjectProperty(device, devicePath, "Reading_Device");

        if (!reading.EnumerateObject().Any())
        {
            throw new PermanentMessageException($"{devicePath}.Reading_Device must contain at least one reading");
        }

        foreach (var property in reading.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name))
            {
                throw new PermanentMessageException($"{devicePath}.Reading_Device contains an empty reading name");
            }

            if (property.Value.ValueKind is JsonValueKind.Array
                or JsonValueKind.Object
                or JsonValueKind.Null
                or JsonValueKind.Undefined)
            {
                throw new PermanentMessageException(
                    $"{devicePath}.Reading_Device.{property.Name} must be a scalar value");
            }
        }
    }

    private static void ValidateSimulationFlags(
        TelemetryEnvelope envelope,
        bool expectedNoSave,
        bool expectedForceDbError)
    {
        if (envelope.NoSave != expectedNoSave)
        {
            throw new PermanentMessageException(
                $"Test case {envelope.TestCase} requires Simulate.NoSave={expectedNoSave.ToString().ToLowerInvariant()}");
        }

        if (envelope.ForceDbError != expectedForceDbError)
        {
            throw new PermanentMessageException(
                $"Test case {envelope.TestCase} requires Simulate.ForceDbError={expectedForceDbError.ToString().ToLowerInvariant()}");
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

    private static void EnsureObject(JsonElement element, string path)
    {
        if (element.ValueKind is not JsonValueKind.Object)
        {
            throw new PermanentMessageException($"{path} must be an object");
        }
    }

    private static string GetRequiredString(JsonElement root, params string[] path)
    {
        var value = GetRequiredValue(root, path);

        if (value.ValueKind is not JsonValueKind.String)
        {
            throw new PermanentMessageException($"{string.Join(".", path)} must be a string");
        }

        var text = value.GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new PermanentMessageException($"{string.Join(".", path)} is empty");
        }

        return text;
    }

    private static string GetRequiredStringProperty(JsonElement root, string parentPath, string propertyName)
    {
        var value = GetRequiredValueProperty(root, parentPath, propertyName);

        if (value.ValueKind is not JsonValueKind.String)
        {
            throw new PermanentMessageException($"{parentPath}.{propertyName} must be a string");
        }

        var text = value.GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new PermanentMessageException($"{parentPath}.{propertyName} is empty");
        }

        return text;
    }

    private static double GetRequiredNumber(JsonElement root, params string[] path)
    {
        var value = GetRequiredValue(root, path);

        if (value.ValueKind is not JsonValueKind.Number)
        {
            throw new PermanentMessageException($"{string.Join(".", path)} must be a number");
        }

        return value.GetDouble();
    }

    private static double GetRequiredNumberProperty(JsonElement root, string parentPath, string propertyName)
    {
        var value = GetRequiredValueProperty(root, parentPath, propertyName);

        if (value.ValueKind is not JsonValueKind.Number)
        {
            throw new PermanentMessageException($"{parentPath}.{propertyName} must be a number");
        }

        return value.GetDouble();
    }

    private static bool GetRequiredBoolean(JsonElement root, params string[] path)
    {
        var value = GetRequiredValue(root, path);

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new PermanentMessageException($"{string.Join(".", path)} must be a boolean")
        };
    }

    private static JsonElement GetRequiredArray(JsonElement root, string propertyName)
    {
        var value = GetRequiredValue(root, propertyName);

        if (value.ValueKind is not JsonValueKind.Array)
        {
            throw new PermanentMessageException($"{propertyName} must be an array");
        }

        return value;
    }

    private static JsonElement GetRequiredArrayProperty(JsonElement root, string parentPath, string propertyName)
    {
        var value = GetRequiredValueProperty(root, parentPath, propertyName);

        if (value.ValueKind is not JsonValueKind.Array)
        {
            throw new PermanentMessageException($"{parentPath}.{propertyName} must be an array");
        }

        return value;
    }

    private static JsonElement GetRequiredObjectProperty(JsonElement root, string parentPath, string propertyName)
    {
        var value = GetRequiredValueProperty(root, parentPath, propertyName);
        EnsureObject(value, $"{parentPath}.{propertyName}");
        return value;
    }

    private static JsonElement GetRequiredValue(JsonElement root, params string[] path)
    {
        var current = root;

        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
            {
                throw new PermanentMessageException($"{string.Join(".", path)} is missing");
            }
        }

        return current;
    }

    private static JsonElement GetRequiredValueProperty(JsonElement root, string parentPath, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            throw new PermanentMessageException($"{parentPath}.{propertyName} is missing");
        }

        return value;
    }

    private sealed record TelemetryEnvelope(
        string MessageId,
        string TestCase,
        bool NoSave,
        bool ForceDbError);
}
