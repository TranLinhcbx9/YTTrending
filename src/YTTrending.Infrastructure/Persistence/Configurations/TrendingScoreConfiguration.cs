namespace YTTrending.Infrastructure.Persistence.Configurations;

public class TrendingScoreConfiguration : IEntityTypeConfiguration<TrendingScore>
{
    public void Configure(EntityTypeBuilder<TrendingScore> builder)
    {
        builder.HasKey(t => t.VideoId);
    }
}
