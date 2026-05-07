namespace EchoDrop.Providers;

public interface IMastodonProvider
{
    Task<string?> PublishAsync(string content, CancellationToken cancellationToken);
}
