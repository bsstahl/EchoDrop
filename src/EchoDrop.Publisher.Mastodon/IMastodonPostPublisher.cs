namespace EchoDrop.Publisher.Mastodon;

public interface IMastodonPostPublisher
{
    Task<string?> PublishAsync(string content, CancellationToken cancellationToken);
}
