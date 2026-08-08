namespace YTTrending.Domain.Entities;

public class Video : AuditableEntity
{
    public int Id { get; set; }

    public required string YoutubeVideoId { get; set; }

    public required int ChannelId { get; set; }

    public required string Title { get; set; }

    public required DateTimeOffset PublishedAt { get; set; }

    public required int DurationSeconds { get; set; }

    public string? Description { get; set; }

    public string? ThumbnailUrl { get; set; }

    /// <summary>Chưa dùng ở Phase 1, chuẩn bị sẵn cho tương lai.</summary>
    public string? Category { get; set; }

    /// <summary>
    /// Chuyển trạng thái đi qua <c>VideoStateRules</c> (Application) — ARCHIVED là trạng thái cuối.
    /// Khai <c>= New</c> tường minh, không dựa vào việc New tình cờ là giá trị 0 của enum.
    /// </summary>
    public VideoStatus Status { get; set; } = VideoStatus.New;

    // Denormalize từ snapshot gần nhất — dashboard filter/sort theo views không phải join.
    public long LatestViews { get; set; }

    public long LatestLikes { get; set; }

    public long LatestComments { get; set; }

    /// <summary>
    /// Đồng hồ đếm <c>ArchivedRetentionDays</c> của Cleanup Job. KHÔNG thay bằng
    /// <c>UpdatedAt</c> — cái đó bị Sync Job đẩy lại mỗi lần đổi title/thumbnail.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public Channel Channel { get; set; } = null!;

    /// <summary>Null tới khi video đủ 2 snapshot để tính được Growth/Velocity.</summary>
    public TrendingScore? TrendingScore { get; set; }

    /// <summary>Null nếu video chưa được bookmark. Saved Videos view lọc `SavedIdea != null`.</summary>
    public SavedIdea? SavedIdea { get; set; }
}
