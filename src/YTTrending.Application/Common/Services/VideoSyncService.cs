using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Common.Options;

namespace YTTrending.Application.Common.Services;

public sealed class VideoSyncService(IVideoRepository videos) : IVideoSyncService
{
    /// <summary>
    /// Đồng bộ tập Shorts được discovery vào video active: archive item hết hạn, refresh item cũ và tạo item mới.
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

        // Số active còn lại sau archive, là nền để enforce tracking cap khi tạo mới.
        var activeCountAfterArchiving = activeVideos.Count - videosToArchive.Count;

        // Fact số record mới được đưa vào TRACKING trong lượt này.
        var newlyTrackedCount = 0;

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

            if (activeCountAfterArchiving + newlyTrackedCount >= tracking.MaxTrackingVideosPerChannel)
                continue;

            videos.Create(CreateVideo(channel.Id, s));
            newlyTrackedCount++;
        }

        return new VideoSyncResult(newlyTrackedCount, existingVideosRefreshedCount, videosToArchive.Count);
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
    /// Tạo video mới từ dữ liệu YouTube và chuyển từ trạng thái NEW sang TRACKING.
    /// </summary>
    private static Video CreateVideo(int channelId, ShortVideoInfo source)
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
        VideoStateRules.StartTracking(video);
        return video;
    }
}
