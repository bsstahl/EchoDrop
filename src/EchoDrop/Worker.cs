using EchoDrop.Services;

namespace EchoDrop;

public sealed class Worker(
    IWorkerEngine workerEngine) : BackgroundService
{
    private readonly IWorkerEngine _workerEngine = workerEngine;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => _workerEngine.ExecuteAsync(stoppingToken);
}
