using EchoDrop;
using EchoDrop.Configuration;
using EchoDrop.Providers;
using EchoDrop.Publisher.Mastodon;
using EchoDrop.Publisher.Mastodon.Configuration;
using EchoDrop.Publishing;
using EchoDrop.Services;
using EchoDrop.Storage;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection(WorkerOptions.SectionName));
builder.Services.Configure<MastodonOptions>(builder.Configuration.GetSection(MastodonOptions.SectionName));

builder.Services.AddSingleton<IScheduledPostRepository, SqliteScheduledPostRepository>();
builder.Services.AddSingleton<ScheduledPostPublisher>();
builder.Services.AddHttpClient<IMastodonPostPublisher, MastodonPostPublisher>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IConfiguration>()
        .GetSection(MastodonOptions.SectionName)
        .Get<MastodonOptions>() ?? new MastodonOptions();

    client.BaseAddress = options.BaseUrl;
});
builder.Services.AddTransient<IPostPublisher, MastodonPostPublisherAdapter>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.Services.GetRequiredService<IScheduledPostRepository>().EnsureSchemaAsync(CancellationToken.None);
await host.RunAsync();
