using YTTrending.Application.Common.Options;

namespace YTTrending.Application.Common.Interfaces;

public interface IShortsDiscoveryService
{
    Task<ShortsDiscoveryResult> DiscoverAsync(
        string uploadsPlaylistId,
        DateTimeOffset now,
        TrackingOptions tracking,
        CancellationToken ct);
}
