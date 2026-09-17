namespace YTTrending.Infrastructure.Persistence.Configurations;

public class TrendingScoreConfiguration : IEntityTypeConfiguration<TrendingScore>
{
    public void Configure(EntityTypeBuilder<TrendingScore> builder)
    {
        // VideoId không khớp pattern Id/{TênClass}Id nên convention không tự nhận
        builder.HasKey(t => t.VideoId);

        // Shared primary key: VideoId vừa là PK vừa là FK
        builder.HasOne<Video>()
               .WithOne(v => v.TrendingScore)
               .HasForeignKey<TrendingScore>(t => t.VideoId);

        builder.Property(t => t.ViewGrowthPct).HasPrecision(10, 2);
        builder.Property(t => t.VelocityPerHour).HasPrecision(14, 2);
        builder.Property(t => t.ViewGrowthNorm).HasPrecision(5, 2);
        builder.Property(t => t.VelocityNorm).HasPrecision(5, 2);
        builder.Property(t => t.Score).HasPrecision(5, 2);
    }
}
