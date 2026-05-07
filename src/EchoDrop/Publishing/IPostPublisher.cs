namespace EchoDrop.Publishing;

public interface IPostPublisher
{
    Task<string?> PublishAsync(string content, CancellationToken cancellationToken);
}
