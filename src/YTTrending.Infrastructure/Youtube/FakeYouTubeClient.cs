using YTTrending.Application.Common.Models;

namespace YTTrending.Infrastructure.YouTube;

/// <summary>Client giả cho Dev/nghiệm thu — không gọi YouTube, không tốn quota. Bật bằng cờ "YouTube:UseFake".</summary>
public sealed class FakeYouTubeClient : IYouTubeClient
{
    public Task<ChannelInfo?> GetChannelAsync(string youtubeHandle, CancellationToken ct)
    {
        var handle = youtubeHandle.StartsWith('@') ? youtubeHandle : $"@{youtubeHandle}";

        // Gõ handle "@notfound" để giả lập channel không tồn tại (test luồng 404)
        ChannelInfo? info = handle.Equals("@notfound", StringComparison.OrdinalIgnoreCase)
            ? null
            : new ChannelInfo($"UC_FAKE_{handle[1..]}", $"Fake Channel {handle}",
                               $"https://www.youtube.com/{handle}");
        return Task.FromResult(info);
    }

    public Task<IReadOnlyList<ShortVideoInfo>> GetRecentShortsAsync(
        string youtubeChannelId, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ShortVideoInfo>>([]);

    public Task<IReadOnlyList<VideoStats>> GetVideoStatsAsync(
        IReadOnlyList<string> youtubeVideoIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<VideoStats>>([]);
}
