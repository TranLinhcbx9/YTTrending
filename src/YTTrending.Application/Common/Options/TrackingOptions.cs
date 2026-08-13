using System.ComponentModel.DataAnnotations;

namespace YTTrending.Application.Common.Options;
public sealed class TrackingOptions
{
    public const string SectionName = "Tracking";

    // Chu kỳ chạy Sync Job (giờ) — cũng chính là snapshot frequency, không tách riêng
    [Range(1, 24)]
    public int SyncIntervalHours { get; init; }

    // Video đăng trong N ngày gần nhất được Discovery coi là "video mới"
    [Range(1, 365)]
    public int RecentDays { get; init; }

    // Số Shorts mới nhất của channel vẫn được xét dù đã ngoài RecentDays (điều kiện OR)
    [Range(1, 100)]
    public int RecentShortsLimit { get; init; }

    // Trần số video/channel đang ở trạng thái TRACKING cùng lúc
    [Range(1, 1000)]
    public int MaxTrackingVideosPerChannel { get; init; }

    // Ngưỡng view tối thiểu để video được đưa vào tracking lúc Discovery
    [Range(0, long.MaxValue)]
    public long MinViewsThreshold { get; init; }

    // Số ngày giữ video ARCHIVED trước khi Cleanup Job soft-delete
    [Range(1, 365)]
    public int ArchivedRetentionDays { get; init; }
}