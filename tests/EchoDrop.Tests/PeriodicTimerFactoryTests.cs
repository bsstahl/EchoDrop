using System.Diagnostics.CodeAnalysis;
using EchoDrop.Services;

namespace EchoDrop.Tests;

[ExcludeFromCodeCoverage]
public sealed class PeriodicTimerFactoryTests
{
    [Fact]
    public async Task Create_ReturnsTimerThatTicks()
    {
        var factory = new PeriodicTimerFactory();

        await using var timer = factory.Create(TimeSpan.FromMilliseconds(10));
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var ticked = await timer.WaitForNextTickAsync(cancellationTokenSource.Token);

        Assert.True(ticked);
    }

    [Fact]
    public async Task DisposeAsync_PreventsFurtherTicks()
    {
        var factory = new PeriodicTimerFactory();
        var timer = factory.Create(TimeSpan.FromMilliseconds(10));

        await timer.DisposeAsync();

        var ticked = await timer.WaitForNextTickAsync(CancellationToken.None);

        Assert.False(ticked);
    }
}
