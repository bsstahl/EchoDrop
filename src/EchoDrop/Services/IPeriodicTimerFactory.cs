namespace EchoDrop.Services;

public interface IPeriodicTimerFactory
{
    IAsyncPeriodicTimer Create(TimeSpan period);
}

public interface IAsyncPeriodicTimer : IAsyncDisposable
{
    ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken);
}
