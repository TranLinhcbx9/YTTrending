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
    : IRequestHandler<SyncChannelCommand, Result>
{
    private readonly TrackingOptions _tracking = trackingOptions.Value;

    public async Task<Result> Handle(SyncChannelCommand cmd, CancellationToken ct)
    {
        if (!syncLock.TryAcquire(cmd.ChannelId))
            return Result.Failure(Error.Conflict(ChannelErrors.SyncInProgress, "Channel đang được sync, thử lại sau."));

        try
        {
            var channel = await channels.GetByIdAsync(cmd.ChannelId, ct);
            if (channel is null)
                return Result.Failure(Error.NotFound(ChannelErrors.NotFound, "Không tìm channel với id được cung cấp."));

            var now = clock.GetUtcNow();
            //if (channel.LastSyncAt is { } lastSync
            //    && now - lastSync < TimeSpan.FromHours(_tracking.SyncIntervalHours))
            //{
            //    return Result.Failure(Error.Conflict(ChannelErrors.SyncTooSoon,
            //        $"Channel vừa sync lúc {lastSync:HH:mm dd/MM}, thử lại sau."));
            //}

            if (channel.UploadsPlaylistId is null)
            {
                channel.UploadsPlaylistId = await youtube.GetUploadsPlaylistIdAsync(channel.YoutubeChannelId, ct);
                if (channel.UploadsPlaylistId is null)
                    return Result.Failure(Error.NotFound(ChannelErrors.NotFound, "Channel không còn tồn tại trên YouTube."));
            }

            var recentShorts = await youtube.GetRecentShortsAsync(
                channel.UploadsPlaylistId, _tracking.RecentShortsLimit, ct);

            var qualifyingShorts = recentShorts
                .Where(s => s.Views >= _tracking.MinViewsThreshold)
                .ToList();
            var fetchedIdSet = qualifyingShorts.Select(s => s.YoutubeVideoId).ToHashSet();

            var activeVideos = await videos.GetActiveByChannelIdAsync(channel.Id, ct);
            var activeByYoutubeId = activeVideos.ToDictionary(v => v.YoutubeVideoId);

            foreach (var s in qualifyingShorts)
            {
                if (activeByYoutubeId.TryGetValue(s.YoutubeVideoId, out var existing))
                {
                    existing.Title = s.Title;
                    existing.ThumbnailUrl = s.ThumbnailUrl;
                    existing.LatestViews = s.Views;
                    existing.LatestLikes = s.Likes;
                    existing.LatestComments = s.Comments;
                }
                else
                {
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
                }
            }

            // Active video rớt khỏi fetch lần này -> archive
            foreach (var existing in activeVideos)
            {
                if (!fetchedIdSet.Contains(existing.YoutubeVideoId))
                    VideoStateRules.Archive(existing, now);
            }

            channel.LastSyncAt = now;
            await uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        finally
        {
            syncLock.Release(cmd.ChannelId);
        }
    }
}
