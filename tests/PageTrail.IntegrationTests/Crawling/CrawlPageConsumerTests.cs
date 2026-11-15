using PageTrail.Infrastructure.Messaging;
using PageTrail.Worker.Processing;
using RabbitMQ.Client;

namespace PageTrail.IntegrationTests.Crawling;

public sealed class CrawlPageConsumerTests
{
    [Fact]
    public void AMessageWithoutAnAttemptHeaderIsTheFirstAttempt()
    {
        Assert.Equal(1, CrawlPageConsumer.AttemptOf(new BasicProperties()));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(2L)]
    public void TheAttemptHeaderIsReadAsIntOrLong(object header)
    {
        var properties = new BasicProperties { Headers = new Dictionary<string, object?> { [CrawlQueues.AttemptHeader] = header } };

        Assert.Equal(2, CrawlPageConsumer.AttemptOf(properties));
    }
}
