using Microsoft.Extensions.DependencyInjection;

namespace ConsumerService.Storage;

public sealed class RepositoryManager : IRepositoryManager
{
    private readonly Lazy<ITelemetryRepository> _telemetryRepository;

    public RepositoryManager(IServiceProvider serviceProvider)
    {
        _telemetryRepository = new Lazy<ITelemetryRepository>(
            () => serviceProvider.GetRequiredService<ITelemetryRepository>());
    }

    public ITelemetryRepository Telemetry => _telemetryRepository.Value;
}
