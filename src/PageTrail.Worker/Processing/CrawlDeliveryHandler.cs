using PageTrail.Infrastructure.Messaging;

namespace PageTrail.Worker.Processing;

public sealed class CrawlDeliveryHandler(CrawlPageProcessor processor, ILogger<CrawlDeliveryHandler> logger)
{
    public const int MaxAttempts = 3;

    public async Task<DeliveryDecision> HandleAsync(ReadOnlyMemory<byte> body, int attempt, CancellationToken cancellationToken)
    {
        var message = CrawlPageMessage.Parse(body);
        if (message is null)
        {
            logger.LogWarning("Unparseable crawl message dead-lettered");
            return DeliveryDecision.DeadLetter;
        }

        try
        {
            return await ProcessAsync(message, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // WHY: any handler failure, transient or not, must go through the retry queue rather than be lost
            return await FailAttemptAsync(message, attempt, ex, cancellationToken);
        }
    }

    private async Task<DeliveryDecision> ProcessAsync(CrawlPageMessage message, CancellationToken cancellationToken)
    {
        if (await processor.ProcessAsync(message, cancellationToken) != ProcessingResult.UnknownPage)
        {
            return DeliveryDecision.Ack;
        }

        logger.LogWarning("Message for unknown page {PageId} dead-lettered", message.PageId);
        return DeliveryDecision.DeadLetter;
    }

    private async Task<DeliveryDecision> FailAttemptAsync(CrawlPageMessage message, int attempt, Exception error, CancellationToken cancellationToken)
    {
        if (attempt < MaxAttempts)
        {
            logger.LogWarning(error, "Page {PageId} failed on attempt {Attempt}; retrying", message.PageId, attempt);
            return DeliveryDecision.Retry(attempt + 1);
        }

        logger.LogError(error, "Page {PageId} failed on its final attempt {Attempt}; dead-lettering", message.PageId, attempt);
        await processor.MarkFailedAsync(message, $"Root page could not be processed after {attempt} attempts", cancellationToken);
        return DeliveryDecision.DeadLetter;
    }
}
