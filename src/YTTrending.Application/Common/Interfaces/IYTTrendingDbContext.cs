namespace YTTrending.Application.Common.Interfaces;

public interface IYTTrendingDbContext
{
    DbSet<Channel> Channels { get; }
    DbSet<Video> Videos { get; }
    DbSet<VideoMetricSnapshot> VideoMetricSnapshots { get; }
    DbSet<TrendingScore> TrendingScores { get; }
    DbSet<SavedIdea> SavedIdeas { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
