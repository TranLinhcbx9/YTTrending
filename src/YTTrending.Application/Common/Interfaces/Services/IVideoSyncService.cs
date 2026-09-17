using YTTrending.Application.Common.Models.Sync;
using YTTrending.Application.Common.Models.Youtube;
using YTTrending.Application.Common.Options;

namespace YTTrending.Application.Common.Interfaces.Services;

public interface IVideoSyncService
{
    Task<VideoSyncResult> ApplyDiscoveryAsync(
        Channel channel,
        IReadOnlyList<ShortVideoInfo> selectedShorts,
        DateTimeOffset now,
        TrackingOptions tracking,
        CancellationToken ct);
}
