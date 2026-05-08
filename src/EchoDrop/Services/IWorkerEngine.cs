namespace EchoDrop.Services;

public interface IWorkerEngine
{
    Task ExecuteAsync(CancellationToken stoppingToken);
}
