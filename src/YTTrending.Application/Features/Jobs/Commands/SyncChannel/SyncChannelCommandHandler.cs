using Microsoft.Extensions.Options;
using YTTrending.Application.Common.Interfaces.Concurrency;
using YTTrending.Application.Common.Interfaces.Integrations;
using YTTrending.Application.Common.Interfaces.Services;
using YTTrending.Application.Common.Options;
using YTTrending.Application.Features.Channels;
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Commands.SyncChannel;

public sealed class SyncChannelCommandHandler(
    IUnitOfWork uow,
    IChannelRepository channels,
    IYouTubeClient youtube,
    IChannelSyncLock syncLock,
    IShortsDiscoveryService shortsDiscovery,
    IVideoSyncService videoSync,
    IOptionsMonitor<TrackingOptions> trackingOptions,
    TimeProvider clock)
    : IRequestHandler<SyncChannelCommand, Result<SyncChannelResultDto>>
{
    public async Task<Result<SyncChannelResultDto>> Handle(SyncChannelCommand cmd, CancellationToken ct)
    {
        // Chặn hai request/job cùng sync một channel, nhưng vẫn cho phép các channel khác chạy song song.
        if (!syncLock.TryAcquire(cmd.ChannelId))
            return Result<SyncChannelResultDto>.Failure(Error.Conflict(ChannelErrors.SyncInProgress, "Channel đang được sync, thử lại sau."));

        try
        {
            // Snapshot config cho trọn lượt sync, tránh thay đổi nóng giữa các bước xử lý.
            var tracking = trackingOptions.CurrentValue;

            // Aggregate channel đang được đồng bộ; các thay đổi sẽ được UnitOfWork lưu ở cuối lượt.
            var channel = await channels.GetByIdAsync(cmd.ChannelId, ct);
            if (channel is null)
                return Result<SyncChannelResultDto>.Failure(Error.NotFound(ChannelErrors.NotFound, "Không tìm channel với id được cung cấp."));

            // Mốc UTC duy nhất dùng nhất quán cho cooldown, cửa sổ tracking và lifecycle trong lượt này.
            var now = clock.GetUtcNow();

            // Lỗi cooldown dự kiến, null nghĩa là channel được phép sync.
            var syncIntervalError = ValidateSyncInterval(channel, now, tracking, cmd.TriggerType);
            if (syncIntervalError is not null)
                return Result<SyncChannelResultDto>.Failure(syncIntervalError);

            // Playlist uploads đã có hoặc vừa được lấy/cached; failure nghĩa là channel không còn trên YouTube.
            var uploadsPlaylistResult = await EnsureUploadsPlaylistIdAsync(channel, ct);
            if (!uploadsPlaylistResult.IsSuccess)
                return Result<SyncChannelResultDto>.Failure(uploadsPlaylistResult.Error!);

            // Đọc/lọc Shorts từ YouTube theo discovery rule; service tự phân trang và dừng khi đủ quota.
            var discovery = await shortsDiscovery.DiscoverAsync(uploadsPlaylistResult.Value, now, tracking, ct);

            // Reconcile tập Shorts đã chọn với lifecycle: archive quá hạn, promote NEW cũ, refresh record active và tạo NEW mới.
            var videoChanges = await videoSync.ApplyDiscoveryAsync(channel, discovery.SelectedShorts, now, tracking, ct);

            channel.LastSyncAt = now;
            await uow.SaveChangesAsync(ct);
            return Result<SyncChannelResultDto>.Success(new(
                FetchedShortsCount: discovery.FetchedShortsCount,
                QualifiedShortsCount: discovery.QualifiedShortsCount,
                NewlyDiscoveredCount: videoChanges.NewlyDiscoveredCount,
                NewlyTrackedCount: videoChanges.NewlyTrackedCount,
                ExistingVideosRefreshedCount: videoChanges.ExistingVideosRefreshedCount,
                ArchivedVideosCount: videoChanges.ArchivedVideosCount));
        }
        finally
        {
            // finally bảo đảm lock luôn được trả, kể cả khi YouTube/DB lỗi hoặc request bị hủy.
            syncLock.Release(cmd.ChannelId);
        }
    }

    /// <summary>
    /// Kiểm tra cooldown của channel; trả lỗi Conflict khi lượt sync trước còn trong khoảng tối thiểu.
    /// </summary>
    private static Error? ValidateSyncInterval(
        Channel channel,
        DateTimeOffset now,
        TrackingOptions tracking,
        SyncRunTriggerType triggerType)
    {
        // Manual là hành động chủ đích nên có cooldown riêng; scheduler theo chu kỳ SyncIntervalHours.
        var cooldown = triggerType == SyncRunTriggerType.Manual
            ? TimeSpan.FromHours(tracking.ManualSyncCooldownHours)
            : TimeSpan.FromHours(tracking.SyncIntervalHours);

        if (channel.LastSyncAt is not { } lastSync || now - lastSync >= cooldown)
            return null;

        return Error.Conflict(ChannelErrors.SyncTooSoon,
            $"Channel vừa sync lúc {lastSync:HH:mm dd/MM}, thử lại sau.");
    }

    /// <summary>
    /// Đảm bảo channel có uploads playlist id để discovery; chỉ gọi YouTube khi giá trị chưa được cache.
    /// </summary>
    private async Task<Result<string>> EnsureUploadsPlaylistIdAsync(Channel channel, CancellationToken ct)
    {
        // Uploads playlist là định danh ổn định do YouTube cung cấp; chỉ cần tra cứu và lưu cache một lần.
        if (channel.UploadsPlaylistId is not null)
            return Result<string>.Success(channel.UploadsPlaylistId);

        channel.UploadsPlaylistId = await youtube.GetUploadsPlaylistIdAsync(channel.YoutubeChannelId, ct);
        return channel.UploadsPlaylistId is null
            ? Result<string>.Failure(Error.NotFound(ChannelErrors.NotFound, "Channel không còn tồn tại trên YouTube."))
            : Result<string>.Success(channel.UploadsPlaylistId);
    }
}
