namespace EchoDrop.Domain.Publishing;

public interface IPostPublisher
{
    Task<string?> PublishAsync(string content, CancellationToken cancellationToken);
}
