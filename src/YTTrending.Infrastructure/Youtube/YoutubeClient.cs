using System.Text.Json;
using Microsoft.Extensions.Options;
using YTTrending.Application.Common.Models;
using YTTrending.Application.Common.Options;

namespace YTTrending.Infrastructure.YouTube;

public sealed class YouTubeClient(HttpClient http, IOptions<TrackingOptions> trackingOptions, IOptions<YouTubeOptions> youtubeOptions) : IYouTubeClient
{
    private readonly TrackingOptions _tracking = trackingOptions.Value;
    private readonly string _apiKey = youtubeOptions.Value.ApiKey;
    public async Task<ChannelInfo?> GetChannelAsync(string youtubeHandle, CancellationToken ct)
    {
        // forHandle bắt buộc có tiền tố "@" (spec YouTube Data API) — chuẩn hoá vì input có thể gõ thiếu
        var handle = youtubeHandle.StartsWith('@') ? youtubeHandle : $"@{youtubeHandle}";
        var url =
            $"channels" +
            $"?part=snippet,statistics,contentDetails" +
            $"&forHandle={handle}" +
            $"&key={_apiKey}";
        using var doc = await GetJsonAsync(url, ct);

        // Handle không tồn tại → YouTube trả 200 nhưng BỎ HẲN property "items" (không phải mảng rỗng)
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            return null;

        var item = items[0];
        var snippet = item.GetProperty("snippet");
        var id = item.GetProperty("id").GetString()!;

        return new ChannelInfo(
            YoutubeChannelId: id,
            Name: snippet.GetProperty("title").GetString()!,
            Url: $"https://www.youtube.com/{handle}");
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
