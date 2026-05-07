using EchoDrop.Configuration;
using EchoDrop.Services;
using Microsoft.Extensions.Options;

namespace EchoDrop;

public sealed class Worker(
    ScheduledPostPublisher scheduledPostPublisher,
    IOptions<WorkerOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly ScheduledPostPublisher _scheduledPostPublisher = scheduledPostPublisher;
    private readonly ILogger<Worker> _logger = logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var publishedCount = await _scheduledPostPublisher.PublishDuePostsAsync(now, stoppingToken).ConfigureAwait(false);

            if (publishedCount > 0)
            {
                _logger.LogInformation("Published {PublishedCount} scheduled post(s).", publishedCount);
            }

            await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
        }
    }
}
