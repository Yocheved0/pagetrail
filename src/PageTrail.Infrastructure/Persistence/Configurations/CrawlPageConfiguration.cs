using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PageTrail.Domain.Jobs;
using PageTrail.Domain.Links;
using PageTrail.Domain.Pages;

namespace PageTrail.Infrastructure.Persistence.Configurations;

internal sealed class CrawlPageConfiguration : IEntityTypeConfiguration<CrawlPage>
{
    private const string UrlKeyHash = "UrlKeyHash";

    public void Configure(EntityTypeBuilder<CrawlPage> builder)
    {
        builder.ToTable("Pages");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Url).HasMaxLength(UrlNormalizer.MaxUrlLength).IsRequired();
        builder.ConfigureUrlWithHash(nameof(CrawlPage.UrlKey), UrlKeyHash);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(p => p.IsRoot);
        builder.HasOne<CrawlJob>().WithMany().HasForeignKey(p => p.JobId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<CrawlPage>().WithMany().HasForeignKey(p => p.ParentPageId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(nameof(CrawlPage.JobId), UrlKeyHash).IsUnique();
        builder.HasIndex(p => new { p.JobId, p.Status });
        builder.HasIndex(p => p.Id).HasFilter("[IsPublished] = 0").HasDatabaseName("IX_Pages_Unpublished");
    }
}
