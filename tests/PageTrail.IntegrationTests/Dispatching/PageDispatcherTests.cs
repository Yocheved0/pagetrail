using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PageTrail.Api.Dispatching;
using PageTrail.Infrastructure.Messaging;
using PageTrail.Infrastructure.Persistence;
using PageTrail.IntegrationTests.Crawling;

namespace PageTrail.IntegrationTests.Dispatching;

[Collection(ApiCollection.Name)]
public sealed class PageDispatcherTests(PageTrailApiFactory factory)
{
    private const string JobUrl = "https://dispatch.test/";
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(15);

    private readonly CrawlHarness _harness = new(factory);

    [Fact]
    public async Task UnpublishedPagesArePublishedAndMarkedPublished()
    {
        await MarkBacklogPublishedAsync();
        var (jobId, pageId) = await _harness.SeedJobAsync(JobUrl);
        var publisher = new RecordingPublisher();
        using var dispatcher = new PageDispatcher(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), publisher, NullLogger<PageDispatcher>.Instance);

        await dispatcher.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => IsPublishedAsync(pageId));
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal([new CrawlPageMessage(jobId, pageId)], publisher.Published);
    }

    [Fact]
    public async Task ADatabaseFailureClosesThePublisherAndALaterTickPublishes()
    {
        await MarkBacklogPublishedAsync();
        var (jobId, pageId) = await _harness.SeedJobAsync(JobUrl);
        var database = new SwitchableDatabase(TestConnectionString());
        var publisher = new RecordingPublisher();
        await using var services = new ServiceCollection()
            .AddDbContext<PageTrailDbContext>(options => options.UseSqlServer(database.ConnectionString))
            .BuildServiceProvider();
        using var dispatcher = new PageDispatcher(
            services.GetRequiredService<IServiceScopeFactory>(), publisher, NullLogger<PageDispatcher>.Instance);

        await dispatcher.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => Task.FromResult(publisher.Closes > 0));
        database.IsUp = true;
        await WaitUntilAsync(() => IsPublishedAsync(pageId));
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.Contains(new CrawlPageMessage(jobId, pageId), publisher.Published);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow + WaitLimit;
        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "Condition not met in time");
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }

    private string TestConnectionString()
    {
        using var db = _harness.CreateDb();
        return db.Database.GetConnectionString()!;
    }

    private async Task MarkBacklogPublishedAsync()
    {
        await using var db = _harness.CreateDb();
        await db.Pages.Where(p => !p.IsPublished).ExecuteUpdateAsync(set => set.SetProperty(p => p.IsPublished, true));
    }

    private async Task<bool> IsPublishedAsync(long pageId)
    {
        await using var db = _harness.CreateDb();
        return await db.Pages.Where(p => p.Id == pageId).Select(p => p.IsPublished).SingleAsync();
    }

    private sealed class SwitchableDatabase(string connectionString)
    {
        private readonly string _missingDatabase =
            new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "PageTrail_Missing" }.ConnectionString;

        public bool IsUp { get; set; }

        public string ConnectionString => IsUp ? connectionString : _missingDatabase;
    }

    private sealed class RecordingPublisher : IPagePublisher
    {
        private int _closes;

        public ConcurrentQueue<CrawlPageMessage> Published { get; } = new();

        public int Closes => Volatile.Read(ref _closes);

        public Task PublishAsync(CrawlPageMessage message, CancellationToken cancellationToken)
        {
            Published.Enqueue(message);
            return Task.CompletedTask;
        }

        public Task CloseAsync()
        {
            Interlocked.Increment(ref _closes);
            return Task.CompletedTask;
        }
    }
}
