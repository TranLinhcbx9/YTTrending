using System.ComponentModel.DataAnnotations;

namespace YTTrending.Application.Common.Options;
public sealed class TrackingOptions
{
    public const string SectionName = "Tracking";

    // Chu kỳ chạy Sync Job (giờ) — cũng chính là snapshot frequency, không tách riêng
    [Range(1, 24)]
    public int SyncIntervalHours { get; init; }

    [Range(0, 24)]
    public float ManualSyncCooldownHours { get; init; }

    // Chu kỳ chạy Metrics Update Job (giờ)
    [Range(1, 24)]
    public int MetricsUpdateIntervalHours { get; init; }

    // Video đăng trong N ngày gần nhất được Discovery coi là "video mới"
    [Range(1, 365)]
    public int RecentDays { get; init; }

    // Số Shorts đạt MinViewsThreshold mới nhất được Discovery chọn làm NEW candidate trong RecentDays.
    [Range(1, 100)]
    public int MaxQualifiedVideosPerChannel { get; init; }

    // Trần số video/channel đang ở trạng thái TRACKING cùng lúc
    [Range(1, 1000)]
    public int MaxTrackingVideosPerChannel { get; init; }

    // Ngưỡng view tối thiểu để video được Discovery lưu làm NEW candidate.
    [Range(0, long.MaxValue)]
    public long MinViewsThreshold { get; init; }

    // Trần thời lượng (giây) để video được coi là Shorts
    [Range(1, 3600)]
    public int ShortsMaxDurationSeconds { get; init; }

    // Số ngày giữ video ARCHIVED trước khi Cleanup Job soft-delete
    [Range(1, 365)]
    public int ArchivedRetentionDays { get; init; }
}
