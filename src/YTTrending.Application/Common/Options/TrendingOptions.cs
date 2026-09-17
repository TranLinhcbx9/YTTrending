using System.ComponentModel.DataAnnotations;

namespace YTTrending.Application.Common.Options;
public sealed class TrendingOptions
{
    public const string SectionName = "Trending";

    // Trọng số % tăng trưởng view (ViewGrowthNorm) trong công thức TrendingScore
    [Range(0, 100)]
    public int ViewGrowthWeight { get; init; }

    // Trọng số tốc độ tăng view — views/giờ (VelocityNorm) trong công thức TrendingScore
    [Range(0, 100)]
    public int VelocityWeight { get; init; }

    // MinViewsThreshold KHÔNG lặp lại ở đây — dùng chung TrackingOptions.MinViewsThreshold
    // để tránh 2 Options lệch giá trị nhau khi tune config (docs/domain/trending-engine.md không dùng field này trong công thức)
}