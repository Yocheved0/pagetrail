using Microsoft.EntityFrameworkCore;
using PageTrail.Domain.Jobs;
using PageTrail.Domain.Pages;

namespace PageTrail.Infrastructure.Persistence;

public sealed class PageTrailDbContext(DbContextOptions<PageTrailDbContext> options) : DbContext(options)
{
    public DbSet<CrawlJob> Jobs => Set<CrawlJob>();

    public DbSet<CrawlPage> Pages => Set<CrawlPage>();

    public DbSet<PageLink> Links => Set<PageLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PageTrailDbContext).Assembly);
}
