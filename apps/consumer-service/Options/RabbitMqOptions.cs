namespace ConsumerService.Options;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public required string HostName { get; init; }
    public int Port { get; init; }
    public string VirtualHost { get; init; } = "/";
    public required string UserName { get; init; }
    public required string Password { get; init; }
    public required string[] Queues { get; init; }
    public ushort PrefetchCount { get; init; }
    public ushort ConsumerDispatchConcurrency { get; init; }
    public bool UseTls { get; init; }
    public string? TlsServerName { get; init; }
    public string? TlsCaCertificatePath { get; init; }

    public static bool Validate(RabbitMqOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.HostName)
            && options.Port > 0
            && !string.IsNullOrWhiteSpace(options.VirtualHost)
            && !string.IsNullOrWhiteSpace(options.UserName)
            && !string.IsNullOrWhiteSpace(options.Password)
            && options.Queues is { Length: > 0 }
            && options.Queues.All(queue => !string.IsNullOrWhiteSpace(queue))
            && options.PrefetchCount > 0
            && options.ConsumerDispatchConcurrency > 0
            && (!options.UseTls
                || (!string.IsNullOrWhiteSpace(options.TlsServerName)
                    && !string.IsNullOrWhiteSpace(options.TlsCaCertificatePath)));
    }
}
