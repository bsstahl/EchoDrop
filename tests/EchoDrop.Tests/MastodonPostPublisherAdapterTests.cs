using EchoDrop.Providers;
using EchoDrop.Publisher.Mastodon;

namespace EchoDrop.Tests;

public sealed class MastodonPostPublisherAdapterTests
{
    [Fact]
    public async Task PublishAsync_ForwardsCallToMastodonPublisher()
    {
        var fakePublisher = new FakeMastodonPostPublisher();
        var sut = new MastodonPostPublisherAdapter(fakePublisher);

        var result = await sut.PublishAsync("Test content", CancellationToken.None);

        Assert.Equal("provider-id", result);
        Assert.Equal(["Test content"], fakePublisher.PublishedContents);
    }

    private sealed class FakeMastodonPostPublisher : IMastodonPostPublisher
    {
        public List<string> PublishedContents { get; } = [];

        public Task<string?> PublishAsync(string content, CancellationToken cancellationToken)
        {
            PublishedContents.Add(content);
            return Task.FromResult<string?>("provider-id");
        }
    }
}
