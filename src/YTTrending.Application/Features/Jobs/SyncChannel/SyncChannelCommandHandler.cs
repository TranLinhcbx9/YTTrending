using Microsoft.Extensions.Options;
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Common.Options;
using YTTrending.Application.Features.Channels;

namespace YTTrending.Application.Features.Jobs.SyncChannel;

public sealed class SyncChannelCommandHandler(
    IUnitOfWork uow,
    IChannelRepository channels,
    IVideoRepository videos,
    IYouTubeClient youtube,
    IChannelSyncLock syncLock,
    IOptions<TrackingOptions> trackingOptions,
    TimeProvider clock)
    : IRequestHandler<SyncChannelCommand, Result<SyncChannelResultDto>>
{
    private readonly TrackingOptions _tracking = trackingOptions.Value;

    public async Task<Result<SyncChannelResultDto>> Handle(SyncChannelCommand cmd, CancellationToken ct)
    {
        // Chặn hai request/job cùng sync một channel, nhưng vẫn cho phép các channel khác chạy song song.
        if (!syncLock.TryAcquire(cmd.ChannelId))
            return Result<SyncChannelResultDto>.Failure(Error.Conflict(ChannelErrors.SyncInProgress, "Channel đang được sync, thử lại sau."));

        try
        {
            var channel = await channels.GetByIdAsync(cmd.ChannelId, ct);
            if (channel is null)
                return Result<SyncChannelResultDto>.Failure(Error.NotFound(ChannelErrors.NotFound, "Không tìm channel với id được cung cấp."));

            var now = clock.GetUtcNow();
            if (channel.LastSyncAt is { } lastSync
                && now - lastSync < TimeSpan.FromHours(_tracking.SyncIntervalHours))
            {
                return Result<SyncChannelResultDto>.Failure(Error.Conflict(ChannelErrors.SyncTooSoon,
                    $"Channel vừa sync lúc {lastSync:HH:mm dd/MM}, thử lại sau."));
            }

            // Uploads playlist là định danh ổn định do YouTube cung cấp; chỉ cần tra cứu và lưu cache một lần.
            if (channel.UploadsPlaylistId is null)
            {
                channel.UploadsPlaylistId = await youtube.GetUploadsPlaylistIdAsync(channel.YoutubeChannelId, ct);
                if (channel.UploadsPlaylistId is null)
                    return Result<SyncChannelResultDto>.Failure(Error.NotFound(ChannelErrors.NotFound, "Channel không còn tồn tại trên YouTube."));
            }

            var trackingWindowStart = now.AddDays(-_tracking.RecentDays);
            var fetchedShorts = new List<ShortVideoInfo>();
            var qualifyingShorts = new List<ShortVideoInfo>();
            string? pageToken = null;

            do
            {
                var page = await youtube.GetRecentShortsPageAsync(channel.UploadsPlaylistId, pageToken, ct);
                var shortsInWindow = page.Shorts
                    .Where(s => s.PublishedAt >= trackingWindowStart)
                    .ToList();

                fetchedShorts.AddRange(shortsInWindow);
                qualifyingShorts.AddRange(shortsInWindow.Where(s => s.Views >= _tracking.MinViewsThreshold));

                // Uploads playlist được sắp mới đến cũ. Đủ quota hoặc đã qua cửa sổ thì không đọc lịch sử sâu hơn.
                if (qualifyingShorts.Count >= _tracking.MaxQualifiedVideosPerChannel
                    || page.NextPageToken is null
                    || page.OldestPlaylistItemPublishedAt < trackingWindowStart)
                    break;

                pageToken = page.NextPageToken;
            }
            while (true);

            var selectedShorts = qualifyingShorts
                .OrderByDescending(s => s.PublishedAt)
                .ThenBy(s => s.YoutubeVideoId)
                .Take(_tracking.MaxQualifiedVideosPerChannel)
                .ToList();

            // ARCHIVED là terminal, nên chỉ đối chiếu với tập active.
            var activeVideos = await videos.GetActiveByChannelIdAsync(channel.Id, ct);
            var activeByYoutubeId = activeVideos.ToDictionary(v => v.YoutubeVideoId);
            var videosToArchive = activeVideos
                .Where(v => v.PublishedAt < trackingWindowStart)
                .ToList();

            foreach (var video in videosToArchive)
                VideoStateRules.Archive(video, now);

            var activeCountAfterArchiving = activeVideos.Count - videosToArchive.Count;
            var newlyTrackedCount = 0;
            var existingVideosRefreshedCount = 0;

            foreach (var s in selectedShorts)
            {
                if (activeByYoutubeId.TryGetValue(s.YoutubeVideoId, out var existing))
                {
                    existing.Title = s.Title;
                    existing.ThumbnailUrl = s.ThumbnailUrl;
                    existing.LatestViews = s.Views;
                    existing.LatestLikes = s.Likes;
                    existing.LatestComments = s.Comments;
                    existingVideosRefreshedCount++;
                    continue;
                }

                if (activeCountAfterArchiving + newlyTrackedCount >= _tracking.MaxTrackingVideosPerChannel)
                    continue;

                var video = new Video
                {
                    YoutubeVideoId = s.YoutubeVideoId,
                    ChannelId = channel.Id,
                    Title = s.Title,
                    PublishedAt = s.PublishedAt,
                    DurationSeconds = s.DurationSeconds,
                    Description = s.Description,
                    ThumbnailUrl = s.ThumbnailUrl,
                    LatestViews = s.Views,
                    LatestLikes = s.Likes,
                    LatestComments = s.Comments,
                };
                VideoStateRules.StartTracking(video);
                videos.Create(video);
                newlyTrackedCount++;
            }

            channel.LastSyncAt = now;
            await uow.SaveChangesAsync(ct);
            return Result<SyncChannelResultDto>.Success(new(
                FetchedShortsCount: fetchedShorts.Count,
                QualifiedShortsCount: qualifyingShorts.Count,
                NewlyTrackedCount: newlyTrackedCount,
                ExistingVideosRefreshedCount: existingVideosRefreshedCount,
                ArchivedVideosCount: videosToArchive.Count));
        }
        finally
        {
            // finally bảo đảm lock luôn được trả, kể cả khi YouTube/DB lỗi hoặc request bị hủy.
            syncLock.Release(cmd.ChannelId);
        }
    }
}
