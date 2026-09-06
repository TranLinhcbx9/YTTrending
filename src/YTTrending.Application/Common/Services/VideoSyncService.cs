using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Common.Options;

namespace YTTrending.Application.Common.Services;

public sealed class VideoSyncService(IVideoRepository videos) : IVideoSyncService
{
    /// <summary>
    /// Đồng bộ tập Shorts được discovery vào lifecycle: archive item hết hạn, promote NEW từ lượt trước,
    /// refresh item cũ và tạo NEW candidate.
    /// </summary>
    public async Task<VideoSyncResult> ApplyDiscoveryAsync(
        Channel channel,
        IReadOnlyList<ShortVideoInfo> selectedShorts,
        DateTimeOffset now,
        TrackingOptions tracking,
        CancellationToken ct)
    {
        // Mốc phân định video active còn tracking và video cần chuyển sang ARCHIVED.
        var trackingWindowStart = now.AddDays(-tracking.RecentDays);

        // ARCHIVED là terminal, nên chỉ đối chiếu với tập active.
        // Toàn bộ video active của channel trước khi lifecycle của lượt này được áp dụng.
        var activeVideos = await videos.GetActiveByChannelIdAsync(channel.Id, ct);

        // Lookup theo YouTube VideoId để refresh đúng record dù metadata video đã thay đổi.
        var activeByYoutubeId = activeVideos.ToDictionary(v => v.YoutubeVideoId);

        // Các active video hết tracking window và sẽ được archive trong lượt này.
        var videosToArchive = activeVideos
            .Where(v => v.PublishedAt < trackingWindowStart)
            .ToList();

        foreach (var video in videosToArchive)
            VideoStateRules.Archive(video, now);

        var newlyTrackedCount = PromoteEligibleNewVideos(activeVideos, tracking);

        // Fact số candidate mới được phát hiện và lưu NEW trong lượt này.
        var newlyDiscoveredCount = 0;

        // Fact số record active cũ được cập nhật metadata/metrics từ YouTube.
        var existingVideosRefreshedCount = 0;

        foreach (var s in selectedShorts)
        {
            if (activeByYoutubeId.TryGetValue(s.YoutubeVideoId, out var existing))
            {
                RefreshExistingVideo(existing, s);
                existingVideosRefreshedCount++;
                continue;
            }

            videos.Create(CreateNewVideo(channel.Id, s));
            newlyDiscoveredCount++;
        }

        return new VideoSyncResult(
            newlyDiscoveredCount,
            newlyTrackedCount,
            existingVideosRefreshedCount,
            videosToArchive.Count);
    }

    /// <summary>
    /// Promote các candidate NEW đã tồn tại trước lượt này. Chỉ TRACKING chiếm quota;
    /// candidate có PublishedAt mới hơn được ưu tiên khi quota không đủ.
    /// </summary>
    private static int PromoteEligibleNewVideos(IReadOnlyList<Video> activeVideos, TrackingOptions tracking)
    {
        var trackingCount = activeVideos.Count(v => v.Status == VideoStatus.Tracking);
        var newlyTrackedCount = 0;

        foreach (var video in activeVideos
                     .Where(v => v.Status == VideoStatus.New && v.LatestViews >= tracking.MinViewsThreshold)
                     .OrderByDescending(v => v.PublishedAt)
                     .ThenBy(v => v.Id))
        {
            if (trackingCount >= tracking.MaxTrackingVideosPerChannel)
                break;

            VideoStateRules.StartTracking(video);
            trackingCount++;
            newlyTrackedCount++;
        }

        return newlyTrackedCount;
    }

    /// <summary>
    /// Cập nhật các field có thể thay đổi sau khi video đã được hệ thống theo dõi.
    /// </summary>
    private static void RefreshExistingVideo(Video video, ShortVideoInfo source)
    {
        video.Title = source.Title;
        video.ThumbnailUrl = source.ThumbnailUrl;
        video.LatestViews = source.Views;
        video.LatestLikes = source.Likes;
        video.LatestComments = source.Comments;
    }

    /// <summary>
    /// Tạo candidate mới từ dữ liệu YouTube. Entity giữ trạng thái NEW cho đến lượt sync thành công kế tiếp.
    /// </summary>
    private static Video CreateNewVideo(int channelId, ShortVideoInfo source)
    {
        // Entity mới được repository theo dõi; caller chịu trách nhiệm add và lưu qua UnitOfWork.
        var video = new Video
        {
            YoutubeVideoId = source.YoutubeVideoId,
            ChannelId = channelId,
            Title = source.Title,
            PublishedAt = source.PublishedAt,
            DurationSeconds = source.DurationSeconds,
            Description = source.Description,
            ThumbnailUrl = source.ThumbnailUrl,
            LatestViews = source.Views,
            LatestLikes = source.Likes,
            LatestComments = source.Comments,
        };
        return video;
    }
}
