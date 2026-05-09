using System.Diagnostics.CodeAnalysis;
using EchoDrop.Configuration;
using EchoDrop.Domain.Interfaces;
using EchoDrop.Domain.Models;
using EchoDrop.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace EchoDrop.Tests;

[ExcludeFromCodeCoverage]
public sealed class WorkerEngineTests
{
    [Fact]
    public async Task ExecuteAsync_UsesMinimumPollIntervalAndPublishesDuePosts()
    {
        var now = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero);
        var postId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var repository = new FakeRepository([[new ScheduledPost(postId, "Hello", now.AddMinutes(-5))]]);
        var provider = new FakeProvider();
        var publisher = new ScheduledPostPublisher(repository, provider, NullLogger<ScheduledPostPublisher>.Instance);
        var timerFactory = new FakeTimerFactory([false]);
        var logger = new ListLogger<WorkerEngine>();
        var engine = new WorkerEngine(
            publisher,
            Options.Create(new WorkerOptions { PollIntervalSeconds = 0 }),
            logger,
            timerFactory,
            new FixedTimeProvider(now));

        await engine.ExecuteAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(1), timerFactory.CreatedPeriod);
        Assert.Equal([now], repository.AsOfCalls);
        Assert.Equal([postId], repository.MarkedPostIds);
        Assert.Equal(["Hello"], provider.PublishedContents);
        Assert.Contains(logger.Messages, message => message.Contains("Published 1 scheduled post(s).", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotLogWhenNothingIsPublished()
    {
        var now = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero);
        var repository = new FakeRepository([[]]);
        var publisher = new ScheduledPostPublisher(repository, new FakeProvider(), NullLogger<ScheduledPostPublisher>.Instance);
        var timerFactory = new FakeTimerFactory([false]);
        var logger = new ListLogger<WorkerEngine>();
        var engine = new WorkerEngine(
            publisher,
            Options.Create(new WorkerOptions { PollIntervalSeconds = 30 }),
            logger,
            timerFactory,
            new FixedTimeProvider(now));

        await engine.ExecuteAsync(CancellationToken.None);

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesUntilTimerStops()
    {
        var now = new DateTimeOffset(2026, 01, 01, 12, 00, 00, TimeSpan.Zero);
        var repository = new FakeRepository([[], []]);
        var publisher = new ScheduledPostPublisher(repository, new FakeProvider(), NullLogger<ScheduledPostPublisher>.Instance);
        var timerFactory = new FakeTimerFactory([true, false]);
        var engine = new WorkerEngine(
            publisher,
            Options.Create(new WorkerOptions { PollIntervalSeconds = 30 }),
            new ListLogger<WorkerEngine>(),
            timerFactory,
            new FixedTimeProvider(now));

        await engine.ExecuteAsync(CancellationToken.None);

        Assert.Equal(2, repository.AsOfCalls.Count);
    }

    [Fact]
    public async Task Worker_DelegatesExecutionToEngine()
    {
        var engine = new FakeWorkerEngine();
        var worker = new Worker(engine);
        using var cancellationTokenSource = new CancellationTokenSource();
        var method = typeof(Worker).GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var executionTask = method.Invoke(worker, [cancellationTokenSource.Token]) as Task;
        Assert.NotNull(executionTask);
        await executionTask;

        Assert.Single(engine.Tokens);
        Assert.Equal(cancellationTokenSource.Token, engine.Tokens[0]);
    }

    private sealed class FakeRepository(IReadOnlyList<IReadOnlyList<ScheduledPost>> duePostsByCall) : IScheduledPostRepository
    {
        private readonly Queue<IReadOnlyList<ScheduledPost>> _duePostsByCall = new(duePostsByCall);

        public List<DateTimeOffset> AsOfCalls { get; } = [];
        public List<Guid> MarkedPostIds { get; } = [];

        public Task EnsureSchemaAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpsertAsync(ScheduledPost post, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpsertAsync(IReadOnlyList<ScheduledPost> posts, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<ScheduledPost>> GetDuePostsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken)
        {
            AsOfCalls.Add(asOfUtc);
            var duePosts = _duePostsByCall.Count > 0 ? _duePostsByCall.Dequeue() : [];
            return Task.FromResult(duePosts);
        }

        public Task MarkAsPublishedAsync(Guid postId, string? providerPostId, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken)
        {
            MarkedPostIds.Add(postId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProvider : IPostPublisher
    {
        public List<string> PublishedContents { get; } = [];

        public Task<string?> PublishAsync(string content, CancellationToken cancellationToken)
        {
            PublishedContents.Add(content);
            return Task.FromResult<string?>("provider-id");
        }
    }

    private sealed class FakeTimerFactory(IReadOnlyList<bool> waitResults) : IPeriodicTimerFactory
    {
        private readonly Queue<bool> _waitResults = new(waitResults);

        public TimeSpan? CreatedPeriod { get; private set; }

        public IAsyncPeriodicTimer Create(TimeSpan period)
        {
            CreatedPeriod = period;
            return new FakeTimer(_waitResults);
        }
    }

    private sealed class FakeTimer(Queue<bool> waitResults) : IAsyncPeriodicTimer
    {
        private readonly Queue<bool> _waitResults = waitResults;

        public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
            => new(_waitResults.Count > 0 && _waitResults.Dequeue());

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private readonly DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public bool IsEnabled(LogLevel logLevel) => true;

        IDisposable? ILogger.BeginScope<TState>(TState state)
            => NullScope.Instance;

        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
            {
                Messages.Add(formatter(state, exception));
            }
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }

    private sealed class FakeWorkerEngine : IWorkerEngine
    {
        public List<CancellationToken> Tokens { get; } = [];

        public Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Tokens.Add(stoppingToken);
            return Task.CompletedTask;
        }
    }
}
