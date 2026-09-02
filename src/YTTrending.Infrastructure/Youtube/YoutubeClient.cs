using System.Text.Json;
using Microsoft.Extensions.Options;
using YTTrending.Application.Common.Models;
using YTTrending.Application.Common.Options;

namespace YTTrending.Infrastructure.YouTube;

public sealed class YouTubeClient(HttpClient http, IOptions<TrackingOptions> trackingOptions, IOptions<YouTubeOptions> youtubeOptions) : IYouTubeClient
{
    private readonly TrackingOptions _tracking = trackingOptions.Value;
    private readonly string _apiKey = youtubeOptions.Value.ApiKey;
    public async Task<ChannelInfo?> GetChannelAsync(string youtubeChannelId, CancellationToken ct)
    {
        var url =
            $"channels" +
            $"?part=snippet,statistics,contentDetails" +
            $"&forHandle={youtubeChannelId}" +
            $"&key={_apiKey}";
        using var doc = await GetJsonAsync(url, ct);

        var items = doc.RootElement.GetProperty("items");
        if (items.GetArrayLength() == 0)
            return null;

        var item = items[0];
        var snippet = item.GetProperty("snippet");
        var id = item.GetProperty("id").GetString()!;

        return new ChannelInfo(
            YoutubeChannelId: id,
            Name: snippet.GetProperty("title").GetString()!,
            Url: $"https://www.youtube.com/@{youtubeChannelId}");
    }

    public Task<IReadOnlyList<ShortVideoInfo>> GetRecentShortsAsync(
        string youtubeChannelId, int limit, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ShortVideoInfo>>([]);

    public Task<IReadOnlyList<VideoStats>> GetVideoStatsAsync(
        IReadOnlyList<string> youtubeVideoIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<VideoStats>>([]);

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        // Lỗi mạng / 5xx / hết quota (403 quotaExceeded) đều ném exception qua EnsureSuccessStatusCode — không nuốt lỗi
        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }
}
