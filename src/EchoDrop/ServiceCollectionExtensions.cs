using EchoDrop.Configuration;
using EchoDrop.Domain.Interfaces;
using EchoDrop.Publisher.Mastodon;
using EchoDrop.Publisher.Mastodon.Configuration;
using EchoDrop.Services;
using EchoDrop.Storage.Sqlite;
using EchoDrop.Storage.Sqlite.Configuration;
using Microsoft.Extensions.Options;

namespace EchoDrop;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureEchoDropOptions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<WorkerOptions>(configuration.GetSection(WorkerOptions.SectionName));
        services.Configure<MastodonOptions>(configuration.GetSection(MastodonOptions.SectionName));

        return services;
    }

    public static IServiceCollection AddEchoDropServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IScheduledPostRepository, SqliteScheduledPostRepository>();
        services.AddSingleton<ScheduledPostPublisher>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IPeriodicTimerFactory, PeriodicTimerFactory>();
        services.AddSingleton<IWorkerEngine, WorkerEngine>();
        services.AddHttpClient<IPostPublisher, MastodonPostPublisher>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MastodonOptions>>().Value;

            client.BaseAddress = options.BaseUrl;
        });

        services.AddHostedService<Worker>();

        return services;
    }
}
