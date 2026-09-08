using YTTrending.Application.Common.Models.Sync;
using YTTrending.Application.Common.Options;

namespace YTTrending.Application.Common.Interfaces.Services;

public interface IShortsDiscoveryService
{
    Task<ShortsDiscoveryResult> DiscoverAsync(
        string uploadsPlaylistId,
        DateTimeOffset now,
        TrackingOptions tracking,
        CancellationToken ct);
}
