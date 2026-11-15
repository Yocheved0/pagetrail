using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PageTrail.Infrastructure.Persistence.Configurations;

internal static class UrlColumns
{
    public const int MaxUrlLength = 2048;

    public static void ConfigureUrlWithHash<T>(this EntityTypeBuilder<T> builder, string urlProperty, string hashProperty)
        where T : class
    {
        builder.Property<string>(urlProperty).HasMaxLength(MaxUrlLength).IsRequired();
        builder.Property<byte[]>(hashProperty)
            .HasMaxLength(32)
            .IsFixedLength()
            .IsRequired()
            .HasComputedColumnSql($"CAST(HASHBYTES('SHA2_256', [{urlProperty}]) AS binary(32))", stored: true);
    }
}
