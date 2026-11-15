using PageTrail.Worker.Processing;

namespace PageTrail.IntegrationTests.Crawling;

public sealed class DeliveryLogScopeTests
{
    private const ulong DeliveryTag = 42;

    [Fact]
    public void ScopeOfAParseableMessageCarriesTheDeliveryTagJobIdAndPageId()
    {
        var jobId = Guid.NewGuid();

        var scope = DeliveryLogScope.For(DeliveryTag, CrawlHarness.BodyOf(jobId, 7));

        Assert.Equal(
            new Dictionary<string, object> { ["DeliveryTag"] = DeliveryTag, ["JobId"] = jobId, ["PageId"] = 7L },
            scope);
    }

    [Fact]
    public void ScopeOfAnUnparseableMessageCarriesOnlyTheDeliveryTag()
    {
        var scope = DeliveryLogScope.For(DeliveryTag, "not json"u8.ToArray());

        Assert.Equal(new Dictionary<string, object> { ["DeliveryTag"] = DeliveryTag }, scope);
    }
}
