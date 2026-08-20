using YTTrending.Application.Common.Models;

namespace YTTrending.Infrastructure.YouTube;

/// <summary>Client giả cho Dev/nghiệm thu — không gọi YouTube, không tốn quota. Bật bằng cờ "YouTube:UseFake".</summary>
public sealed class FakeYouTubeClient : IYouTubeClient
{
    public Task<ChannelInfo?> GetChannelAsync(string youtubeChannelId, CancellationToken ct)
    {
        ChannelInfo? info = youtubeChannelId.StartsWith("UC", StringComparison.Ordinal)
            ? new ChannelInfo(youtubeChannelId, $"Fake Channel {youtubeChannelId}",
                              $"https://www.youtube.com/channel/{youtubeChannelId}")
            : null;
        return Task.FromResult(info);
    }

    public Task<IReadOnlyList<ShortVideoInfo>> GetRecentShortsAsync(
        string youtubeChannelId, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ShortVideoInfo>>([]);

    public Task<IReadOnlyList<VideoStats>> GetVideoStatsAsync(
        IReadOnlyList<string> youtubeVideoIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<VideoStats>>([]);
}
