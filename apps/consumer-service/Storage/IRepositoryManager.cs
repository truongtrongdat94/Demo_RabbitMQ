namespace ConsumerService.Storage;

public interface IRepositoryManager
{
    ITelemetryRepository Telemetry { get; }
}
