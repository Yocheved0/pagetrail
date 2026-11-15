using PageTrail.Infrastructure.Messaging;

namespace PageTrail.Worker.Processing;

public static class DeliveryLogScope
{
    public static Dictionary<string, object> For(ulong deliveryTag, ReadOnlyMemory<byte> body)
    {
        var scope = new Dictionary<string, object> { ["DeliveryTag"] = deliveryTag };
        if (CrawlPageMessage.Parse(body) is { } message)
        {
            scope["JobId"] = message.JobId;
            scope["PageId"] = message.PageId;
        }

        return scope;
    }
}
