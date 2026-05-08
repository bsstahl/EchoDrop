using EchoDrop.Configuration;
using Microsoft.Extensions.Options;

namespace EchoDrop.Services;

public sealed class WorkerEngine(
    ScheduledPostPublisher scheduledPostPublisher,
    IOptions<WorkerOptions> options,
    ILogger<WorkerEngine> logger,
    IPeriodicTimerFactory periodicTimerFactory,
    TimeProvider timeProvider) : IWorkerEngine
{
    private readonly ScheduledPostPublisher _scheduledPostPublisher = scheduledPostPublisher;
    private readonly ILogger<WorkerEngine> _logger = logger;
    private readonly IPeriodicTimerFactory _periodicTimerFactory = periodicTimerFactory;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = _periodicTimerFactory.Create(_pollInterval);
        await using (timer.ConfigureAwait(false))
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = _timeProvider.GetUtcNow();
                var publishedCount = await _scheduledPostPublisher.PublishDuePostsAsync(now, stoppingToken).ConfigureAwait(false);

                if (publishedCount > 0)
                {
                    _logger.LogInformation("Published {PublishedCount} scheduled post(s).", publishedCount);
                }

                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    break;
                }
            }
        }
    }
}
