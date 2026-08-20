namespace YTTrending.Domain.Entities;

public class VideoMetricSnapshot
{
    /// <summary>BIGINT — bảng này tăng nhanh nhất hệ thống.</summary>
    public int Id { get; set; }

    public required int VideoId { get; set; }

    public required long Views { get; set; }

    public required long Likes { get; set; }

    public required long Comments { get; set; }

    /// <summary>
    /// Dữ liệu nghiệp vụ (thời điểm ĐO số liệu), không phải audit field —
    /// set tường minh ở chỗ tạo snapshot, không để interceptor điền hộ.
    /// </summary>
    public required DateTimeOffset SnapshotAt { get; set; }
}
