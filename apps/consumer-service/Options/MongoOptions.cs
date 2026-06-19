namespace ConsumerService.Options;

public sealed class MongoOptions
{
    public const string SectionName = "Mongo";

    public required string ConnectionString { get; init; }
    public required string DatabaseName { get; init; }
    public required string CollectionName { get; init; }

    public static bool Validate(MongoOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.ConnectionString)
            && !string.IsNullOrWhiteSpace(options.DatabaseName)
            && !string.IsNullOrWhiteSpace(options.CollectionName);
    }
}

