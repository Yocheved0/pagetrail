using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PageTrail.Infrastructure.Messaging;
using PageTrail.Infrastructure.Persistence;
using RabbitMQ.Client.Exceptions;

namespace PageTrail.Api.Dispatching;

public sealed class PageDispatcher(
    IServiceScopeFactory scopes,
    IPagePublisher publisher,
    ILogger<PageDispatcher> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await publisher.CloseAsync();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PublishBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (IsOutage(ex))
            {
                logger.LogWarning(ex, "Page dispatch failed; retrying on the next tick");
                await publisher.CloseAsync();
            }
        }
    }

    private static bool IsOutage(Exception ex) =>
        ex is RabbitMQClientException or IOException or DbException or DbUpdateException
        // WHY: EF Core wraps transient SQL Server errors such as a refused login in InvalidOperationException
        || ex is InvalidOperationException { InnerException: DbException };

    private async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PageTrailDbContext>();
        var pages = await db.Pages.Where(p => !p.IsPublished).OrderBy(p => p.Id).Take(BatchSize).ToListAsync(cancellationToken);

        foreach (var page in pages)
        {
            await publisher.PublishAsync(new CrawlPageMessage(page.JobId, page.Id), cancellationToken);
            page.MarkPublished();
            logger.LogInformation("Page {PageId} of job {JobId} published", page.Id, page.JobId);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
