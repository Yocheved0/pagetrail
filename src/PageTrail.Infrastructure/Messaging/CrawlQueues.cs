using RabbitMQ.Client;

namespace PageTrail.Infrastructure.Messaging;

public static class CrawlQueues
{
    public const string Pages = "crawl.pages";

    public const string Retry = "crawl.pages.retry";

    public const string DeadLetter = "crawl.pages.dlq";

    public const string AttemptHeader = "x-attempt";

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await DeclareDurableAsync(channel, Pages, arguments: null, cancellationToken);
        await DeclareDurableAsync(channel, Retry, RetryArguments(), cancellationToken);
        await DeclareDurableAsync(channel, DeadLetter, arguments: null, cancellationToken);
    }

    private static Dictionary<string, object?> RetryArguments() => new()
    {
        ["x-message-ttl"] = (int)RetryDelay.TotalMilliseconds,
        ["x-dead-letter-exchange"] = "",
        ["x-dead-letter-routing-key"] = Pages,
    };

    private static Task DeclareDurableAsync(IChannel channel, string queue, Dictionary<string, object?>? arguments, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments, cancellationToken: cancellationToken);
}
