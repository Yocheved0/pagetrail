using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PageTrail.Domain.Pages;

namespace PageTrail.Infrastructure.Persistence.Configurations;

internal sealed class PageLinkConfiguration : IEntityTypeConfiguration<PageLink>
{
    private const string TargetUrlHash = "TargetUrlHash";

    public void Configure(EntityTypeBuilder<PageLink> builder)
    {
        builder.ToTable("Links");
        builder.HasKey(l => l.Id);
        builder.ConfigureUrlWithHash(nameof(PageLink.TargetUrl), TargetUrlHash);
        builder.HasOne<CrawlPage>().WithMany().HasForeignKey(l => l.SourcePageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(nameof(PageLink.SourcePageId), TargetUrlHash).IsUnique();
    }
}
