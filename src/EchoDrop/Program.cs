using EchoDrop;
using EchoDrop.Configuration;
using EchoDrop.Domain.Interfaces;
using EchoDrop.Publisher.Mastodon;
using EchoDrop.Publisher.Mastodon.Configuration;
using EchoDrop.Services;
using EchoDrop.Storage.Sqlite;
using EchoDrop.Storage.Sqlite.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));
builder.Services.Configure<MastodonOptions>(builder.Configuration.GetSection(MastodonOptions.SectionName));

builder.Services.AddSingleton<IScheduledPostRepository, SqliteScheduledPostRepository>();
builder.Services.AddSingleton<ScheduledPostPublisher>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IPeriodicTimerFactory, PeriodicTimerFactory>();
builder.Services.AddSingleton<IWorkerEngine, WorkerEngine>();
builder.Services.AddHttpClient<IPostPublisher, MastodonPostPublisher>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IConfiguration>()
        .GetSection(MastodonOptions.SectionName)
        .Get<MastodonOptions>() ?? new MastodonOptions();

    client.BaseAddress = options.BaseUrl;
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.Services.GetRequiredService<IScheduledPostRepository>().EnsureSchemaAsync(CancellationToken.None).ConfigureAwait(false);
await host.RunAsync().ConfigureAwait(false);
