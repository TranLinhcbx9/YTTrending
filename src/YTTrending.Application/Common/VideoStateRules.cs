namespace YTTrending.Application.Common;

/// <summary>
/// Vòng đời video NEW → TRACKING → ARCHIVED. Entity là property bag thuần nên rule
/// nằm ở đây — <b>quy ước, không phải ràng buộc compiler</b>: gán thẳng
/// <c>video.Status = ...</c> vẫn compile được. Mọi chỗ đổi trạng thái phải đi qua đây.
/// </summary>
public static class VideoStateRules
{
    /// <summary>Chỉ đi được từ NEW.</summary>
    public static void StartTracking(Video video)
    {
        if (video.Status != VideoStatus.New)
            throw new InvalidOperationException(
                $"StartTracking chỉ hợp lệ từ NEW, hiện tại là {video.Status}.");

        video.Status = VideoStatus.Tracking;
    }

    /// <summary>
    /// ARCHIVED là trạng thái cuối. Cố tình KHÔNG giới hạn trạng thái nguồn —
    /// NEW → ARCHIVED thẳng là hợp lệ (video rớt khỏi recent list trước khi kịp track),
    /// chặn thêm sẽ làm video kẹt vĩnh viễn ở NEW.
    /// </summary>
    public static void Archive(Video video, DateTimeOffset now)
    {
        if (video.Status == VideoStatus.Archived)
            throw new InvalidOperationException("Video đã ARCHIVED — không có đường quay lại.");

        video.Status = VideoStatus.Archived;
        video.ArchivedAt = now;
    }
}
