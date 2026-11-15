using System.Text.Json;

namespace PageTrail.Infrastructure.Messaging;

public sealed record CrawlPageMessage(Guid JobId, long PageId)
{
    public static CrawlPageMessage? Parse(ReadOnlyMemory<byte> body)
    {
        try
        {
            return JsonSerializer.Deserialize<CrawlPageMessage>(body.Span);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
