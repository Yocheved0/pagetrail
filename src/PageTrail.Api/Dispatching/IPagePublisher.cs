using PageTrail.Infrastructure.Messaging;

namespace PageTrail.Api.Dispatching;

public interface IPagePublisher
{
    Task PublishAsync(CrawlPageMessage message, CancellationToken cancellationToken);

    Task CloseAsync();
}
