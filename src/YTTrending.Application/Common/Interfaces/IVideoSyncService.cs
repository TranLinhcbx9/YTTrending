using YTTrending.Application.Common.Options;

namespace YTTrending.Application.Common.Interfaces;

public interface IVideoSyncService
{
    Task<VideoSyncResult> ApplyDiscoveryAsync(
        Channel channel,
        IReadOnlyList<ShortVideoInfo> selectedShorts,
        DateTimeOffset now,
        TrackingOptions tracking,
        CancellationToken ct);
}
