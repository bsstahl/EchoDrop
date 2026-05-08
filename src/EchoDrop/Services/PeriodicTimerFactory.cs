namespace EchoDrop.Services;

public sealed class PeriodicTimerFactory : IPeriodicTimerFactory
{
    public IAsyncPeriodicTimer Create(TimeSpan period)
        => new PeriodicTimerAdapter(period);

    private sealed class PeriodicTimerAdapter(TimeSpan period) : IAsyncPeriodicTimer
    {
        private readonly PeriodicTimer _timer = new(period);

        public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
            => _timer.WaitForNextTickAsync(cancellationToken);

        public ValueTask DisposeAsync()
        {
            _timer.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
