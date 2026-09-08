using YTTrending.Application.Common.Interfaces.Integrations;
using YTTrending.Application.Common.Interfaces.Services;
using YTTrending.Application.Common.Models.Sync;
using YTTrending.Application.Common.Models.Youtube;
using YTTrending.Application.Common.Options;

namespace YTTrending.Application.Common.Services;

public sealed class ShortsDiscoveryService(IYouTubeClient youtube) : IShortsDiscoveryService
{
    /// <summary>
    /// Đọc tuần tự uploads playlist và trả các Shorts mới nhất thỏa discovery rule trong tracking window.
    /// </summary>
    public async Task<ShortsDiscoveryResult> DiscoverAsync(
        string uploadsPlaylistId,
        DateTimeOffset now,
        TrackingOptions tracking,
        CancellationToken ct)
    {
        // Mốc cũ nhất mà một video vẫn còn đủ điều kiện được discovery trong lượt sync này.
        var trackingWindowStart = now.AddDays(-tracking.RecentDays);

        // Tập Shorts trong cửa sổ đã đọc từ YouTube, dùng để báo fact cho FE.
        var fetchedShorts = new List<ShortVideoInfo>();

        // Tập con đạt ngưỡng view trước khi chọn tối đa quota video mới nhất.
        var qualifyingShorts = new List<ShortVideoInfo>();

        // Token trang kế tiếp; null biểu thị request trang đầu tiên.
        string? pageToken = null;

        do
        {
            // Một trang uploads theo thứ tự mới đến cũ do YouTube trả về.
            var page = await youtube.GetRecentShortsPageAsync(uploadsPlaylistId, pageToken, ct);

            // Chỉ giữ item còn trong tracking window; item cũ không được dùng để bù quota.
            var shortsInWindow = page.Shorts
                .Where(s => s.PublishedAt >= trackingWindowStart)
                .ToList();

            fetchedShorts.AddRange(shortsInWindow);
            qualifyingShorts.AddRange(shortsInWindow.Where(s => s.Views >= tracking.MinViewsThreshold));

            // Uploads playlist được sắp mới đến cũ. Đủ quota hoặc đã qua cửa sổ thì không đọc lịch sử sâu hơn.
            if (qualifyingShorts.Count >= tracking.MaxQualifiedVideosPerChannel
                || page.NextPageToken is null
                || page.OldestPlaylistItemPublishedAt < trackingWindowStart)
                break;

            pageToken = page.NextPageToken;
        }
        while (true);

        // Tập cuối cùng để reconcile, có thứ tự xác định khi PublishedAt trùng nhau.
        var selectedShorts = qualifyingShorts
            .OrderByDescending(s => s.PublishedAt)
            .ThenBy(s => s.YoutubeVideoId)
            .Take(tracking.MaxQualifiedVideosPerChannel)
            .ToList();

        return new ShortsDiscoveryResult(fetchedShorts.Count, qualifyingShorts.Count, selectedShorts);
    }
}
