using Microsoft.Extensions.DependencyInjection;

namespace ConsumerService.Storage;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryRepository, MongoTelemetryRepository>();
        services.AddSingleton<IRepositoryManager, RepositoryManager>();

        return services;
    }
}
