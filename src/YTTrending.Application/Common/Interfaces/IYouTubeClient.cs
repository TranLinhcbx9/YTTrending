namespace YTTrending.Application.Common.Interfaces;

/// <summary>
/// Interface cho gọi API ngoài (YouTube) — tách biệt khỏi Repository pattern dùng cho data access nội bộ,
/// xem docs/decisions.md mục 6.
/// Chữ ký còn là TẠM — sửa khi làm Discovery thật, xem docs/decisions.md mục Batch 4.
/// </summary>
public interface IYouTubeClient
{
    /// <summary>
    /// Nhận YouTube handle (@name, có hoặc không có dấu "@" đều được) — KHÔNG phải channel ID.
    /// Trả null khi channel không tồn tại — handler map thành Error.NotFound.
    /// Lỗi hạ tầng (hết quota, mạng, 5xx) vẫn ném exception, đúng phân loại ở docs/decisions.md.
    /// </summary>
    Task<ChannelInfo?> GetChannelAsync(string youtubeHandle, CancellationToken ct);

    /// <summary>
    /// Uploads playlist id của một channel đã biết ChannelId — chỉ dùng để bù cho channel lưu
    /// trước khi Channel.UploadsPlaylistId tồn tại. Trả null khi channel không còn trên YouTube.
    /// <para>
    /// Đường chính KHÔNG đi qua đây: AddChannel đã lấy sẵn giá trị này từ GetChannelAsync.
    /// </para>
    /// </summary>
    Task<string?> GetUploadsPlaylistIdAsync(string youtubeChannelId, CancellationToken ct);

    /// <summary>
    /// Một trang Shorts của channel, thứ tự thời gian giảm dần.
    /// <para>
    /// Nhận thẳng <paramref name="uploadsPlaylistId"/> (lấy từ Channel trong DB), KHÔNG nhận
    /// channel id — client không tự đi tra cứu state, caller đưa đủ thứ nó cần.
    /// </para>
    /// <para>
    /// <paramref name="pageToken"/> là token do trang trước trả về; null nghĩa là trang đầu.
    /// Client KHÔNG đọc config Discovery và không lọc theo ngày hoặc view; các rule nghiệp vụ
    /// đó do handler áp dụng.
    /// </para>
    /// </summary>
    Task<ShortsPage> GetRecentShortsPageAsync(
        string uploadsPlaylistId, string? pageToken, CancellationToken ct);

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
