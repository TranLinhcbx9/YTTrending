namespace YTTrending.Domain.Entities;

/// <summary>
/// 1 row / video, ghi đè mỗi lần Metrics Update Job chạy — không giữ lịch sử theo thời gian.
/// Cần xem biến thiên thì tính lại từ <see cref="VideoMetricSnapshot"/>.
/// </summary>
public class TrendingScore
{
    /// <summary>Vừa là PK vừa là FK (1-1 với Video) — bảng này KHÔNG có cột <c>Id</c> riêng.</summary>
    public required int VideoId { get; set; }

    /// <summary>% tăng trưởng view giữa 2 snapshot gần nhất.</summary>
    public decimal ViewGrowthPct { get; set; }

    /// <summary>Tốc độ tăng view (views/giờ).</summary>
    public decimal VelocityPerHour { get; set; }

    public decimal ViewGrowthNorm { get; set; }

    public decimal VelocityNorm { get; set; }

    /// <summary>
    /// Đặt tên <c>Score</c> chứ không phải <c>TrendingScore</c>: member không được
    /// trùng tên class chứa nó (CS0542). Cột DB vì thế cũng là <c>score</c>.
    /// </summary>
    public decimal Score { get; set; }

    public required DateTimeOffset CalculatedAt { get; set; }
}
