using EchoDrop;
using EchoDrop.Configuration;
using EchoDrop.Providers;
using EchoDrop.Services;
using EchoDrop.Storage;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<MastodonOptions>(builder.Configuration.GetSection(MastodonOptions.SectionName));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));

builder.Services.AddSingleton<IScheduledPostRepository, SqliteScheduledPostRepository>();
builder.Services.AddSingleton<ScheduledPostPublisher>();
builder.Services.AddHttpClient<IMastodonProvider, MastodonProvider>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IConfiguration>()
        .GetSection(MastodonOptions.SectionName)
        .Get<MastodonOptions>() ?? new MastodonOptions();

    client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.Services.GetRequiredService<IScheduledPostRepository>().EnsureSchemaAsync(CancellationToken.None);
await host.RunAsync();
