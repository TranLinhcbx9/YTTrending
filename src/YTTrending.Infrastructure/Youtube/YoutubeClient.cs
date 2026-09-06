using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Options;
using YTTrending.Application.Common.Models;
using YTTrending.Application.Common.Options;

namespace YTTrending.Infrastructure.YouTube;

public sealed class YouTubeClient(HttpClient http, IOptions<TrackingOptions> trackingOptions, IOptions<YouTubeOptions> youtubeOptions) : IYouTubeClient
{
    /// <summary>
    /// Giới hạn cứng của YouTube Data API: videos.list nhận tối đa 50 id mỗi request.
    /// Không phải thông số cấu hình được — nâng lên sẽ nhận 400 từ API.
    /// </summary>
    private const int MaxVideoIdsPerRequest = 50;
    private const int MaxPlaylistItemsPerRequest = 50;

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
            Url: $"https://www.youtube.com/{handle}",
            // contentDetails đã nằm sẵn trong part của request này — lấy luôn, không tốn thêm quota
            UploadsPlaylistId: ReadUploadsPlaylistId(item));
    }

    public async Task<string?> GetUploadsPlaylistIdAsync(string youtubeChannelId, CancellationToken ct)
    {
        var url = $"channels?part=contentDetails&id={youtubeChannelId}&key={_apiKey}";
        using var doc = await GetJsonAsync(url, ct);

        if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
            return null;

        return ReadUploadsPlaylistId(items[0]);
    }

    public async Task<ShortsPage> GetRecentShortsPageAsync(
        string uploadsPlaylistId, string? pageToken, CancellationToken ct)
    {
        // Lấy một trang uploads; handler quyết định có cần trang tiếp theo theo tracking window hay không.
        var pageTokenQuery = pageToken is null ? string.Empty : $"&pageToken={Uri.EscapeDataString(pageToken)}";
        var playlistUrl = $"playlistItems?part=snippet&playlistId={uploadsPlaylistId}&maxResults={MaxPlaylistItemsPerRequest}{pageTokenQuery}&key={_apiKey}";
        using var playlistDoc = await GetJsonAsync(playlistUrl, ct);

        var playlistItems = playlistDoc.RootElement.TryGetProperty("items", out var items)
            ? items.EnumerateArray().ToArray()
            : [];
        var videoIds = playlistItems
            .Select(i => i.GetProperty("snippet").GetProperty("resourceId").GetProperty("videoId").GetString()!)
            .ToList();
        DateTimeOffset? oldestPlaylistItemPublishedAt = playlistItems.Length == 0
            ? null
            : playlistItems[^1].GetProperty("snippet").GetProperty("publishedAt").GetDateTimeOffset();
        var nextPageToken = playlistDoc.RootElement.TryGetProperty("nextPageToken", out var nextPageTokenElement)
            ? nextPageTokenElement.GetString()
            : null;

        if (videoIds.Count == 0)
            return new ShortsPage([], nextPageToken, oldestPlaylistItemPublishedAt);

        // 2. Lấy chi tiết (duration, statistics, snippet) cho từng video, chia lô theo giới hạn API
        var result = new List<ShortVideoInfo>();
        foreach (var chunk in videoIds.Chunk(MaxVideoIdsPerRequest))
        {
            var idsParam = string.Join(",", chunk);
            // liveStreamingDetails đi chung request — thêm part KHÔNG tốn thêm quota (quota tính theo request)
            var videosUrl = $"videos?part=contentDetails,statistics,snippet,liveStreamingDetails&id={idsParam}&key={_apiKey}";
            using var videosDoc = await GetJsonAsync(videosUrl, ct);

            foreach (var v in videosDoc.RootElement.GetProperty("items").EnumerateArray())
            {
                // Livestream/premiere không bao giờ là Shorts — loại trước, khỏi phải parse duration rác của chúng
                if (v.TryGetProperty("liveStreamingDetails", out _))
                    continue;

                // Livestream đang chạy trả "P0D", video vừa xử lý xong chưa có duration trả "PT0S" —
                // cả hai parse ra 0s (không ném), nên chốt chặn là điều kiện == 0 bên dưới.
                // try/catch ở đây phòng chuỗi ISO-8601 rác ngoài dự kiến: bỏ 1 video còn hơn hỏng cả lượt sync.
                var rawDuration = v.GetProperty("contentDetails").GetProperty("duration").GetString()!;
                TimeSpan duration;
                try
                {
                    duration = XmlConvert.ToTimeSpan(rawDuration);
                }
                catch (FormatException)
                {
                    continue;
                }

                // duration 0 = chưa xử lý xong, chưa biết dài bao nhiêu → bỏ qua lượt này, lần sync sau lấy lại
                if (duration.TotalSeconds == 0 || duration.TotalSeconds > _tracking.ShortsMaxDurationSeconds)
                    continue; // không phải Shorts, loại

                var snippet = v.GetProperty("snippet");
                var stats = v.GetProperty("statistics");

                result.Add(new ShortVideoInfo(
                    YoutubeVideoId: v.GetProperty("id").GetString()!,
                    Title: snippet.GetProperty("title").GetString()!,
                    PublishedAt: snippet.GetProperty("publishedAt").GetDateTimeOffset(),
                    DurationSeconds: (int)duration.TotalSeconds,
                    Description: snippet.TryGetProperty("description", out var d) ? d.GetString() : null,
                    ThumbnailUrl: snippet.TryGetProperty("thumbnails", out var t) && t.TryGetProperty("high", out var h)
                        ? h.GetProperty("url").GetString()
                        : null,
                    Views: stats.TryGetProperty("viewCount", out var vc) ? long.Parse(vc.GetString()!) : 0,
                    Likes: stats.TryGetProperty("likeCount", out var lc) ? long.Parse(lc.GetString()!) : 0,
                    Comments: stats.TryGetProperty("commentCount", out var cc) ? long.Parse(cc.GetString()!) : 0));
            }
        }

        return new ShortsPage(result, nextPageToken, oldestPlaylistItemPublishedAt);
    }

    public Task<IReadOnlyList<VideoStats>> GetVideoStatsAsync(
        IReadOnlyList<string> youtubeVideoIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<VideoStats>>([]);

    /// <summary>
    /// Đọc contentDetails.relatedPlaylists.uploads của một item channels.list.
    /// Trả null thay vì ném khi thiếu — channel không có uploads playlist là bất thường nhưng
    /// không đáng làm hỏng cả lượt AddChannel; caller coi như "chưa biết" và hỏi lại sau.
    /// </summary>
    private static string? ReadUploadsPlaylistId(JsonElement channelItem)
        => channelItem.TryGetProperty("contentDetails", out var cd)
           && cd.TryGetProperty("relatedPlaylists", out var rp)
           && rp.TryGetProperty("uploads", out var uploads)
            ? uploads.GetString()
            : null;

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        // Lỗi mạng / 5xx / hết quota (403 quotaExceeded) đều ném exception qua EnsureSuccessStatusCode — không nuốt lỗi
        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }
}
