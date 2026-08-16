namespace YTTrending.Application.Common.Interfaces;

/// <summary>
/// Một trong 2 interface duy nhất Application expose ra ngoài (cùng IYTTrendingDbContext).
/// Chữ ký còn là TẠM — sửa khi làm Discovery thật, xem docs/decisions.md mục Batch 4.
/// </summary>
public interface IYouTubeClient
{
    /// <summary>
    /// Trả null khi channel không tồn tại — handler map thành Error.NotFound.
    /// Lỗi hạ tầng (hết quota, mạng, 5xx) vẫn ném exception, đúng phân loại ở docs/decisions.md.
    /// </summary>
    Task<ChannelInfo?> GetChannelAsync(string youtubeChannelId, CancellationToken ct);

    /// <summary>
    /// N Shorts mới nhất của channel, thứ tự thời gian giảm dần.
    /// <para>
    /// <paramref name="limit"/> do handler truyền từ TrackingOptions.RecentShortsLimit —
    /// client KHÔNG đọc config.
    /// </para>
    /// <para>
    /// Cả 2 vế OR của Discovery rule (đăng trong RecentDays HOẶC nằm trong N mới nhất) lẫn
    /// ngưỡng MinViewsThreshold đều do handler lọc trên kết quả này — toàn bộ rule nghiệp vụ
    /// nằm một chỗ, FakeYouTubeClient không phải chép lại rule nào.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ShortVideoInfo>> GetRecentShortsAsync(
        string youtubeChannelId, int limit, CancellationToken ct);

    /// <summary>
    /// Nhận list dài tùy ý — implementation tự chia lô 50 (trần của videos.list), caller không
    /// cần biết con số đó.
    /// <para>
    /// ⚠️ Video đã xoá/private KHÔNG có trong kết quả → list trả về có thể ngắn hơn input.
    /// Đối chiếu theo YoutubeVideoId, đừng theo index.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<VideoStats>> GetVideoStatsAsync(
        IReadOnlyList<string> youtubeVideoIds, CancellationToken ct);
}
