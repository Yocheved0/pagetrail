using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PageTrail.Infrastructure.Messaging;
using PageTrail.Infrastructure.Persistence;
using RabbitMQ.Client;

namespace PageTrail.Api.Dispatching;

public sealed class PageDispatcher(
    IServiceScopeFactory scopes,
    IOptions<RabbitMqOptions> rabbitMq,
    ILogger<PageDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connection = await rabbitMq.Value.CreateConnectionFactory().CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            stoppingToken);
        await CrawlQueues.DeclareAsync(channel, stoppingToken);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await PublishBatchAsync(channel, stoppingToken);
        }
    }

    private async Task PublishBatchAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PageTrailDbContext>();
        var pages = await db.Pages.Where(p => !p.IsPublished).OrderBy(p => p.Id).Take(BatchSize).ToListAsync(cancellationToken);

        foreach (var page in pages)
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(new CrawlPageMessage(page.JobId, page.Id));
            await channel.BasicPublishAsync(
                exchange: "", routingKey: CrawlQueues.Pages, mandatory: true, new BasicProperties { Persistent = true }, body, cancellationToken);
            page.MarkPublished();
            logger.LogInformation("Page {PageId} of job {JobId} published", page.Id, page.JobId);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
