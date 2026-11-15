using RabbitMQ.Client;

namespace PageTrail.Infrastructure.Messaging;

public static class CrawlQueues
{
    public const string Pages = "crawl.pages";

    public static Task DeclareAsync(IChannel channel, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(Pages, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
}
