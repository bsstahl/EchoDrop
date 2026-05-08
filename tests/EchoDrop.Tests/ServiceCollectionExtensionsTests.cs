using EchoDrop.Configuration;
using EchoDrop.Domain.Interfaces;
using EchoDrop.Publisher.Mastodon;
using EchoDrop.Publisher.Mastodon.Configuration;
using EchoDrop.Services;
using EchoDrop.Storage.Sqlite;
using EchoDrop.Storage.Sqlite.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace EchoDrop.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void ConfigureEchoDropOptions_BindsAllConfiguredSections()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{DatabaseOptions.SectionName}:ConnectionString"] = "Data Source=config.db",
            [$"{WorkerOptions.SectionName}:PollIntervalSeconds"] = "9",
            [$"{MastodonOptions.SectionName}:BaseUrl"] = "https://example.social",
            [$"{MastodonOptions.SectionName}:AccessToken"] = "token-123"
        });

        var services = new ServiceCollection();

        services.ConfigureEchoDropOptions(configuration);

        using var provider = services.BuildServiceProvider();
        var databaseOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var workerOptions = provider.GetRequiredService<IOptions<WorkerOptions>>().Value;
        var mastodonOptions = provider.GetRequiredService<IOptions<MastodonOptions>>().Value;

        Assert.Equal("Data Source=config.db", databaseOptions.ConnectionString);
        Assert.Equal(9, workerOptions.PollIntervalSeconds);
        Assert.Equal(new Uri("https://example.social"), mastodonOptions.BaseUrl);
        Assert.Equal("token-123", mastodonOptions.AccessToken);
    }

    [Fact]
    public void ConfigureEchoDropOptions_ThrowsWhenArgumentsAreNull()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.ConfigureEchoDropOptions(null!, configuration));
        Assert.Throws<ArgumentNullException>(() => services.ConfigureEchoDropOptions(null!));
    }

    [Fact]
    public void AddEchoDropServices_RegistersCoreServicesAndPublisherClient()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{MastodonOptions.SectionName}:BaseUrl"] = "https://mastodon.example"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureEchoDropOptions(configuration);

        services.AddEchoDropServices();

        using var provider = services.BuildServiceProvider();
        Assert.IsType<SqliteScheduledPostRepository>(provider.GetRequiredService<IScheduledPostRepository>());
        Assert.IsType<ScheduledPostPublisher>(provider.GetRequiredService<ScheduledPostPublisher>());
        Assert.IsType<PeriodicTimerFactory>(provider.GetRequiredService<IPeriodicTimerFactory>());
        Assert.IsType<WorkerEngine>(provider.GetRequiredService<IWorkerEngine>());

        var hostedServices = provider.GetServices<IHostedService>();
        Assert.Contains(hostedServices, hostedService => hostedService is Worker);

        var publisher = provider.GetRequiredService<IPostPublisher>();
        var mastodonPublisher = Assert.IsType<MastodonPostPublisher>(publisher);
        var httpClientField = typeof(MastodonPostPublisher).GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(httpClientField);
        var httpClient = Assert.IsType<HttpClient>(httpClientField.GetValue(mastodonPublisher));
        Assert.Equal(new Uri("https://mastodon.example"), httpClient.BaseAddress);
    }

    [Fact]
    public void AddEchoDropServices_ThrowsWhenArgumentsAreNull()
    {
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddEchoDropServices(null!));
    }

    private static IConfiguration BuildConfiguration(IDictionary<string, string?>? entries = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(entries ?? new Dictionary<string, string?>())
            .Build();
}
