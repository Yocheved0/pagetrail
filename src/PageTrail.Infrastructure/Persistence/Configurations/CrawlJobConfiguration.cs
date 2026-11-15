using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PageTrail.Domain.Jobs;

namespace PageTrail.Infrastructure.Persistence.Configurations;

internal sealed class CrawlJobConfiguration : IEntityTypeConfiguration<CrawlJob>
{
    public void Configure(EntityTypeBuilder<CrawlJob> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).ValueGeneratedNever();
        builder.Property(j => j.Url).HasMaxLength(UrlColumns.MaxUrlLength).IsRequired();
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(j => j.FailureReason).HasMaxLength(1024);
        builder.Ignore(j => j.IsFinished);
        builder.HasIndex(j => j.CreatedAt).IsDescending();
    }
}
