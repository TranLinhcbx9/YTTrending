# Background Job thật — Sync Channel Job + Metrics Update Job

> ⚠️ **FILE TẠM — không commit, xoá sau khi xong toàn bộ batch.**
> Plan mode session gốc: `C:\Users\Linh\.claude\plans\m-chinh-lai-plan-happy-shamir.md` — thoát tự động giữa chừng, không có nội dung. File này là bản đầy đủ duy nhất.

## Context

Mục 7 (`setup-base.md`) vốn định làm khung rỗng trước (`SyncChannelsCommand` chỉ log, chưa đụng YouTube thật) — hoãn từ 24/08/2026 vì chưa có API key. Giờ có key thật, gộp thẳng thành bản thật, và gộp luôn Metrics Update Job (không nằm trong Mục 7 gốc, nhưng cần cùng `IYouTubeClient` thật nên làm chung 1 đợt cho đỡ tách lần nữa).

Quyết định nền tảng (endpoint nào, quota tính sao, config gì) đã chốt ở [`../../docs/decisions.md`](../../docs/decisions.md) mục **"Background job thật"** (01/09/2026) — đọc trước khi code, plan này không lặp lại phần lý do.

5 batch, làm tuần tự — mỗi batch build + nghiệm thu được độc lập trước khi sang batch sau.

⚠️ Code dưới đây đã đối chiếu với state hiện tại của repo lúc viết plan (đọc `IYouTubeClient`, entities, `VideoStateRules`, repository interfaces, feature `Channels` làm mẫu). 2 lệch đã phát hiện so với bản plan gốc:
- `TrackingOptions.cs` **đã có sẵn** `MetricsUpdateIntervalHours` + `ShortsMaxDurationSeconds` — Batch A chỉ còn việc tách `JobOptions`.
- File client giả hiện tại là `src/YTTrending.Infrastructure/Youtube/YoutubeClient.cs` (thư mục **`Youtube`** chữ thường, không phải `YouTube`) nhưng bên trong lại là **class `YouTubeClient`** — còn `DependencyInjection.cs` đang resolve `FakeYouTubeClient` (namespace `YTTrending.Infrastructure.YouTube`, thư mục hoa). Tên file/class/namespace hiện KHÔNG khớp nhau — kiểm tra lại thực tế trong IDE trước khi paste, code mẫu dưới đây giả định giữ nguyên class `FakeYouTubeClient` ở chỗ cũ và thêm class thật `YouTubeClient` mới cạnh nó.

---

## Batch A — Config & Options groundwork

**Mục đích:** thêm sẵn các tham số config mà job thật cần (interval update metrics, ngưỡng phân biệt Shorts, bật/tắt riêng từng job) — chưa đụng logic, chỉ chuẩn bị chỗ chứa.

- Thêm 2 config mới vào Tracking options: khoảng cách giữa các lần update metrics (giờ), và ngưỡng thời lượng tối đa để coi là Shorts (giây). **(Đã có sẵn trong `TrackingOptions.cs` hiện tại — bỏ qua bước này.)**
- Tách cờ bật/tắt job thành 2 cờ riêng (sync job và metrics-update job), thay vì 1 cờ chung.
- Cập nhật `appsettings.json` (Production-ish) và `appsettings.Development.json` với giá trị tương ứng — Development vẫn giữ tinh thần kill-switch (tắt mặc định).
- Set API key thật vào user-secrets (chạy tay, không phải code).

⚠️ Chưa đổi cờ "dùng fake YouTube client" — để nguyên tới hết Batch B, đổi sớm sẽ vỡ DI vì chưa có class thật.

### Code — `src/YTTrending.Application/Common/Options/JobOptions.cs`

```csharp
namespace YTTrending.Application.Common.Options;
public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    // Kill-switch riêng cho Sync Channel Job — tắt ở Development để đỡ tốn quota YouTube Data API lúc debug (A5)
    public bool SyncEnabled { get; init; }

    // Kill-switch riêng cho Metrics Update Job — tách khỏi SyncEnabled để bật/tắt độc lập
    public bool MetricsUpdateEnabled { get; init; }
}
```

### Code — `src/YTTrending.API/appsettings.json` (đoạn `Jobs`)

```json
"Jobs": {
  "SyncEnabled": true,
  "MetricsUpdateEnabled": true
},
```

### Code — `src/YTTrending.API/appsettings.Development.json` (đoạn `Jobs`)

```json
"Jobs": {
  "SyncEnabled": false,
  "MetricsUpdateEnabled": false
}
```

### Lệnh chạy tay (không phải code)

```
cd src/YTTrending.API
dotnet user-secrets set "YouTube:ApiKey" "<key thật>"
```

✅ Nghiệm thu: build sạch, chạy app khởi động bình thường.

---

## Batch B — `YouTubeClient` thật

**Mục đích:** thay implementation giả của `IYouTubeClient` bằng bản gọi API YouTube thật, giữ nguyên chữ ký interface để phần còn lại của hệ thống không cần đổi gì.

- Lấy thông tin 1 channel từ YouTube.
- Lấy danh sách Shorts gần đây của 1 channel: tra playlist upload của channel → lấy danh sách video trong đó → lấy chi tiết từng video → lọc ra video nào thực sự là Shorts (dựa theo thời lượng) → map sang model nội bộ.
- Lấy thống kê (views/likes/comments) cho một danh sách video theo lô, gộp kết quả lại.
- Lỗi hạ tầng (mạng, lỗi server, hết quota) → để lỗi nổi lên (ném exception), không nuốt âm thầm.
- Đăng ký implementation thật vào DI (nhánh hiện đang bỏ trống).

### Code — `src/YTTrending.Infrastructure/YouTube/YouTubeClientReal.cs` (file mới, cạnh `FakeYouTubeClient`)

> Đặt tên file `YouTubeClientReal.cs` / class `YouTubeClientReal` để tránh đụng tên với class `YouTubeClient` đang nằm sẵn trong `Youtube/YoutubeClient.cs` (xem cảnh báo lệch tên ở đầu file plan) — đổi tên lại cho khớp convention thật khi paste, miễn nhất quán với `DependencyInjection.cs`.

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml;
using Microsoft.Extensions.Options;
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Common.Models;
using YTTrending.Application.Common.Options;

namespace YTTrending.Infrastructure.YouTube;

public sealed class YouTubeClientReal(HttpClient http, IOptions<TrackingOptions> trackingOptions, IOptions<YouTubeOptions> youtubeOptions)
    : IYouTubeClient
{
    private readonly TrackingOptions _tracking = trackingOptions.Value;
    private readonly string _apiKey = youtubeOptions.Value.ApiKey;

    public async Task<ChannelInfo?> GetChannelAsync(string youtubeChannelId, CancellationToken ct)
    {
        var url = $"channels?part=snippet&id={youtubeChannelId}&key={_apiKey}";
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
            Url: $"https://www.youtube.com/channel/{id}");
    }

    public async Task<IReadOnlyList<ShortVideoInfo>> GetRecentShortsAsync(
        string youtubeChannelId, int limit, CancellationToken ct)
    {
        // 1. Lấy uploads playlist id của channel
        var channelUrl = $"channels?part=contentDetails&id={youtubeChannelId}&key={_apiKey}";
        using var channelDoc = await GetJsonAsync(channelUrl, ct);
        var channelItems = channelDoc.RootElement.GetProperty("items");
        if (channelItems.GetArrayLength() == 0)
            return [];

        var uploadsPlaylistId = channelItems[0]
            .GetProperty("contentDetails")
            .GetProperty("relatedPlaylists")
            .GetProperty("uploads")
            .GetString()!;

        // 2. Lấy N video id mới nhất trong playlist đó
        var playlistUrl = $"playlistItems?part=snippet&playlistId={uploadsPlaylistId}&maxResults={limit}&key={_apiKey}";
        using var playlistDoc = await GetJsonAsync(playlistUrl, ct);

        var videoIds = playlistDoc.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(i => i.GetProperty("snippet").GetProperty("resourceId").GetProperty("videoId").GetString()!)
            .ToList();

        if (videoIds.Count == 0)
            return [];

        // 3. Lấy chi tiết (duration, statistics, snippet) cho từng video, chia lô 50
        var result = new List<ShortVideoInfo>();
        foreach (var chunk in videoIds.Chunk(50))
        {
            var idsParam = string.Join(",", chunk);
            var videosUrl = $"videos?part=contentDetails,statistics,snippet&id={idsParam}&key={_apiKey}";
            using var videosDoc = await GetJsonAsync(videosUrl, ct);

            foreach (var v in videosDoc.RootElement.GetProperty("items").EnumerateArray())
            {
                var duration = XmlConvert.ToTimeSpan(v.GetProperty("contentDetails").GetProperty("duration").GetString()!);
                if (duration.TotalSeconds > _tracking.ShortsMaxDurationSeconds)
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

        return result;
    }

    public async Task<IReadOnlyList<VideoStats>> GetVideoStatsAsync(
        IReadOnlyList<string> youtubeVideoIds, CancellationToken ct)
    {
        var result = new List<VideoStats>();

        foreach (var chunk in youtubeVideoIds.Chunk(50))
        {
            var idsParam = string.Join(",", chunk);
            var url = $"videos?part=statistics&id={idsParam}&key={_apiKey}";
            using var doc = await GetJsonAsync(url, ct);

            foreach (var v in doc.RootElement.GetProperty("items").EnumerateArray())
            {
                var stats = v.GetProperty("statistics");
                result.Add(new VideoStats(
                    YoutubeVideoId: v.GetProperty("id").GetString()!,
                    Views: stats.TryGetProperty("viewCount", out var vc) ? long.Parse(vc.GetString()!) : 0,
                    Likes: stats.TryGetProperty("likeCount", out var lc) ? long.Parse(lc.GetString()!) : 0,
                    Comments: stats.TryGetProperty("commentCount", out var cc) ? long.Parse(cc.GetString()!) : 0));
            }
        }

        return result;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        // Lỗi mạng / 5xx / hết quota (403 quotaExceeded) đều ném exception qua EnsureSuccessStatusCode — không nuốt lỗi
        using var response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }
}
```

> Nếu chưa có `YouTubeOptions` (class Options riêng cho section `YouTube:ApiKey`/`YouTube:UseFake`) — kiểm tra `Common/Options/` trước khi paste, có thể project đang đọc trực tiếp qua `IConfiguration["YouTube:ApiKey"]` thay vì Options pattern. Sửa constructor cho khớp cách thật.

### Code — `src/YTTrending.Infrastructure/DependencyInjection.cs` (đổi nhánh `else`)

```csharp
if (configuration.GetValue("YouTube:UseFake", true))
    services.AddSingleton<IYouTubeClient, FakeYouTubeClient>();
else
    services.AddHttpClient<IYouTubeClient, YouTubeClientReal>(c =>
        c.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/"));
```

✅ Nghiệm thu: bật cờ dùng client thật, gọi API tạo channel với 1 channel ID thật → dữ liệu lưu đúng như lấy từ YouTube thật.

---

## Batch C — `SyncChannelsCommand` (Discovery thật)

**Mục đích:** job định kỳ quét các channel đang theo dõi, phát hiện video mới, và archive video đã rớt khỏi danh sách gần đây — hiện thực hoá đúng luồng đã mô tả ở `discovery-engine.md`.

- Bổ sung 2 khả năng còn thiếu ở tầng repository: lấy danh sách channel đang bật theo dõi; và lấy video đã tồn tại theo danh sách ID để so khớp (cần bản có thể sửa trực tiếp rồi lưu, không phải bản chỉ đọc).
- Viết command + handler cho luồng sync:
  1. Chốt 1 mốc thời gian dùng chung cho cả lượt chạy.
  2. Với mỗi channel đang bật: lấy danh sách Shorts gần đây, lọc theo ngưỡng view tối thiểu.
  3. So khớp với DB theo video ID: video mới → tạo mới ở trạng thái bắt đầu theo dõi; video đã có → cập nhật các field có thể đổi (tên, thumbnail); video có trong DB nhưng lần này không thấy nữa → nếu đang theo dõi thì chuyển sang archived.
  4. Cập nhật thời điểm sync gần nhất của channel.
  5. Lưu tất cả thay đổi 1 lần cuối lượt.

### Code — `IChannelRepository.cs` (thêm method)

```csharp
public interface IChannelRepository : IRepository<Channel>
{
    Task<bool> ExistsByYoutubeIdAsync(string youtubeChannelId, CancellationToken ct);
    Task<PagedResult<Channel>> GetPagedAsync(ChannelFilter filter, CancellationToken ct);
    Task<List<Channel>> GetEnabledAsync(CancellationToken ct);
}
```

### Code — `ChannelRepository.cs` (thêm implementation)

```csharp
public Task<List<Channel>> GetEnabledAsync(CancellationToken ct) =>
    Set.Where(c => c.IsEnabled).ToListAsync(ct);
```

### Code — `IVideoRepository.cs` (thêm method)

```csharp
public interface IVideoRepository : IRepository<Video>
{
    Task<PagedResult<Video>> GetPagedAsync(VideoFilter filter, CancellationToken ct);
    Task<Video?> GetByIdWithChannelAsync(int id, CancellationToken ct);
    Task<List<Video>> GetByYoutubeIdsAsync(IReadOnlyList<string> youtubeVideoIds, CancellationToken ct);
}
```

### Code — `VideoRepository.cs` (thêm implementation)

```csharp
// KHÔNG AsNoTracking — bước so khớp ở handler cần sửa field trực tiếp rồi SaveChanges
public Task<List<Video>> GetByYoutubeIdsAsync(IReadOnlyList<string> youtubeVideoIds, CancellationToken ct) =>
    Set.Where(v => youtubeVideoIds.Contains(v.YoutubeVideoId)).ToListAsync(ct);
```

### Code — `src/YTTrending.Application/Features/Jobs/SyncChannels/SyncChannelsCommand.cs`

```csharp
namespace YTTrending.Application.Features.Jobs.SyncChannels;

public sealed record SyncChannelsCommand : IRequest<Result>;
```

### Code — `src/YTTrending.Application/Features/Jobs/SyncChannels/SyncChannelsCommandHandler.cs`

```csharp
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace YTTrending.Application.Features.Jobs.SyncChannels;

public sealed class SyncChannelsCommandHandler(
    IUnitOfWork uow,
    IChannelRepository channels,
    IVideoRepository videos,
    IYouTubeClient youtube,
    IOptions<TrackingOptions> trackingOptions,
    TimeProvider clock)
    : IRequestHandler<SyncChannelsCommand, Result>
{
    private readonly TrackingOptions _tracking = trackingOptions.Value;

    public async Task<Result> Handle(SyncChannelsCommand cmd, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var enabledChannels = await channels.GetEnabledAsync(ct);

        foreach (var channel in enabledChannels)
        {
            var recentShorts = await youtube.GetRecentShortsAsync(
                channel.YoutubeChannelId, _tracking.RecentShortsLimit, ct);

            var qualifyingShorts = recentShorts
                .Where(s => s.Views >= _tracking.MinViewsThreshold)
                .ToList();

            var fetchedIds = qualifyingShorts.Select(s => s.YoutubeVideoId).ToList();
            var existingVideos = await videos.GetByYoutubeIdsAsync(fetchedIds, ct);
            var existingByYoutubeId = existingVideos.ToDictionary(v => v.YoutubeVideoId);

            // Video mới hoặc đã có -> tạo/cập nhật
            foreach (var s in qualifyingShorts)
            {
                if (existingByYoutubeId.TryGetValue(s.YoutubeVideoId, out var existing))
                {
                    existing.Title = s.Title;
                    existing.ThumbnailUrl = s.ThumbnailUrl;
                }
                else
                {
                    var video = new Video
                    {
                        YoutubeVideoId = s.YoutubeVideoId,
                        ChannelId = channel.Id,
                        Title = s.Title,
                        PublishedAt = s.PublishedAt,
                        DurationSeconds = s.DurationSeconds,
                        Description = s.Description,
                        ThumbnailUrl = s.ThumbnailUrl,
                        LatestViews = s.Views,
                        LatestLikes = s.Likes,
                        LatestComments = s.Comments,
                    };
                    VideoStateRules.StartTracking(video);
                    videos.Create(video);
                }
            }

            // Video đang TRACKING trong DB nhưng lần fetch này không còn thấy -> archive
            var fetchedIdSet = fetchedIds.ToHashSet();
            var trackingVideosOfChannel = await videos.GetByYoutubeIdsAsync(
                existingByYoutubeId.Keys.ToList(), ct); // đã tracked ở trên, tái dùng existingVideos cũng được

            foreach (var existing in existingVideos)
            {
                if (existing.Status == VideoStatus.Tracking && !fetchedIdSet.Contains(existing.YoutubeVideoId))
                    VideoStateRules.Archive(existing, now);
            }

            channel.LastSyncAt = now;
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

> ⚠️ Chỗ "video có trong DB nhưng rớt khỏi fetch lần này" ở trên chỉ archive video **đã từng match `existingByYoutubeId`** — tức video đang track thuộc channel đó nhưng KHÔNG nằm trong `qualifyingShorts` lần này lại không bị bắt, vì `existingVideos` chỉ được load theo `fetchedIds`. Đây là chỗ cần suy nghĩ thêm lúc code thật: có thể cần load thêm toàn bộ video `Tracking` của channel (không chỉ theo `fetchedIds`) để so sánh đầy đủ tập "trước" vs "sau". Plan gốc ghi nhận bước 3 nhưng code mẫu này chưa xử lý trọn vẹn — kiểm tra lại logic trước khi paste thẳng.

✅ Nghiệm thu: gọi tay qua endpoint job (Batch E) — video mới xuất hiện đúng trạng thái; video rớt khỏi danh sách gần đây chuyển archived đúng.

---

## Batch D — `UpdateVideoMetricsCommand` + Trending Score

**Mục đích:** job định kỳ cập nhật số liệu (views/likes/comments) cho video đang theo dõi, lưu lại lịch sử snapshot, và tính điểm trending để phục vụ dashboard.

- Bổ sung khả năng lấy toàn bộ video đang ở trạng thái theo dõi (không phân trang, khác với API dùng cho UI).
- Viết command + handler cho luồng update metrics:
  1. Chốt 1 mốc thời gian dùng chung cho cả lượt chạy.
  2. Lấy toàn bộ video đang theo dõi → gọi API lấy thống kê mới nhất (client tự chia lô).
  3. Khớp kết quả trả về với video theo ID (không theo thứ tự, vì kết quả có thể thiếu so với input).
  4. Video khớp được: cập nhật số liệu mới nhất trên video, đồng thời ghi thêm 1 snapshot mới vào lịch sử.
  5. Video không khớp được (đã bị xoá/private trên YouTube): case này chưa có rule sẵn — **tự quyết hoặc hỏi lại lúc code, không tự bịa**. Code mẫu dưới đây tạm chọn "bỏ qua, chờ lượt sau" (đơn giản nhất, không archive vội vì có thể chỉ là lỗi tạm thời từ YouTube) — đây là lựa chọn CHƯA xác nhận với bạn, đổi lại nếu quyết định khác.
  6. Tính điểm trending cho video đã có đủ ít nhất 2 snapshot trở lên, ghi đè kết quả (không giữ lịch sử điểm).
  7. Lưu tất cả thay đổi 1 lần cuối lượt.
- Số liệu/snapshot/trending score dùng thẳng `DbContext` qua handler trong code mẫu này (đơn giản hơn so với tạo thêm 2 repository chỉ để insert/upsert) — **xác nhận lại với convention thật của project lúc code**, vì mọi entity khác trong repo đều có repository riêng.

### Code — `IVideoRepository.cs` (thêm method)

```csharp
Task<List<Video>> GetTrackingAsync(CancellationToken ct);
```

### Code — `VideoRepository.cs` (thêm implementation)

```csharp
public Task<List<Video>> GetTrackingAsync(CancellationToken ct) =>
    Set.Where(v => v.Status == VideoStatus.Tracking).ToListAsync(ct);
```

### Code — `src/YTTrending.Application/Features/Jobs/UpdateVideoMetrics/UpdateVideoMetricsCommand.cs`

```csharp
namespace YTTrending.Application.Features.Jobs.UpdateVideoMetrics;

public sealed record UpdateVideoMetricsCommand : IRequest<Result>;
```

### Code — `src/YTTrending.Application/Features/Jobs/UpdateVideoMetrics/UpdateVideoMetricsCommandHandler.cs`

```csharp
using Microsoft.Extensions.Options;
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Common.Options;

namespace YTTrending.Application.Features.Jobs.UpdateVideoMetrics;

public sealed class UpdateVideoMetricsCommandHandler(
    IUnitOfWork uow,
    IVideoRepository videos,
    IYouTubeClient youtube,
    IOptions<TrendingOptions> trendingOptions,
    TimeProvider clock)
    : IRequestHandler<UpdateVideoMetricsCommand, Result>
{
    private readonly TrendingOptions _trending = trendingOptions.Value;

    public async Task<Result> Handle(UpdateVideoMetricsCommand cmd, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var trackingVideos = await videos.GetTrackingAsync(ct);

        var stats = await youtube.GetVideoStatsAsync(
            trackingVideos.Select(v => v.YoutubeVideoId).ToList(), ct);
        var statsByYoutubeId = stats.ToDictionary(s => s.YoutubeVideoId);

        var newSnapshots = new List<VideoMetricSnapshot>();

        foreach (var video in trackingVideos)
        {
            if (!statsByYoutubeId.TryGetValue(video.YoutubeVideoId, out var s))
                continue; // video đã xoá/private trên YouTube -> bỏ qua lượt này, KHÔNG archive (lựa chọn tạm, xác nhận lại)

            video.LatestViews = s.Views;
            video.LatestLikes = s.Likes;
            video.LatestComments = s.Comments;

            newSnapshots.Add(new VideoMetricSnapshot
            {
                VideoId = video.Id,
                Views = s.Views,
                Likes = s.Likes,
                Comments = s.Comments,
                SnapshotAt = now,
            });
        }

        // newSnapshots insert qua DbContext thẳng (hoặc repository riêng nếu convention project yêu cầu)
        // db.VideoMetricSnapshots.AddRange(newSnapshots);

        await RecalculateTrendingScoresAsync(trackingVideos, now, ct);

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task RecalculateTrendingScoresAsync(List<Video> trackingVideos, DateTimeOffset now, CancellationToken ct)
    {
        // Với mỗi video có >= 2 snapshot: lấy 2 snapshot gần nhất từ VideoMetricSnapshots
        // (query riêng theo VideoId, OrderByDescending(SnapshotAt).Take(2))
        var candidates = new List<(Video Video, decimal ViewGrowth, decimal Velocity)>();

        foreach (var video in trackingVideos)
        {
            // var lastTwo = await db.VideoMetricSnapshots
            //     .Where(s => s.VideoId == video.Id)
            //     .OrderByDescending(s => s.SnapshotAt)
            //     .Take(2)
            //     .ToListAsync(ct);
            // if (lastTwo.Count < 2) continue;
            //
            // var (prev, curr) = (lastTwo[1], lastTwo[0]);
            // var hoursBetween = (decimal)(curr.SnapshotAt - prev.SnapshotAt).TotalHours;
            // var viewGrowth = prev.Views == 0 ? 0 : (curr.Views - prev.Views) / (decimal)prev.Views * 100;
            // var velocity = hoursBetween == 0 ? 0 : (curr.Views - prev.Views) / hoursBetween;
            //
            // candidates.Add((video, viewGrowth, velocity));
        }

        if (candidates.Count == 0)
            return;

        var minGrowth = candidates.Min(c => c.ViewGrowth);
        var maxGrowth = candidates.Max(c => c.ViewGrowth);
        var minVelocity = candidates.Min(c => c.Velocity);
        var maxVelocity = candidates.Max(c => c.Velocity);

        foreach (var (video, viewGrowth, velocity) in candidates)
        {
            var growthNorm = maxGrowth == minGrowth ? 0 : (viewGrowth - minGrowth) / (maxGrowth - minGrowth) * 100;
            var velocityNorm = maxVelocity == minVelocity ? 0 : (velocity - minVelocity) / (maxVelocity - minVelocity) * 100;
            var score = (growthNorm * _trending.ViewGrowthWeight + velocityNorm * _trending.VelocityWeight) / 100;

            // Upsert 1 row vào TrendingScores — tìm theo VideoId, có thì update, chưa có thì tạo mới
            // var existing = await db.TrendingScores.FindAsync(new object[] { video.Id }, ct);
            // if (existing is null)
            // {
            //     db.TrendingScores.Add(new TrendingScore
            //     {
            //         VideoId = video.Id, ViewGrowthPct = viewGrowth, VelocityPerHour = velocity,
            //         ViewGrowthNorm = growthNorm, VelocityNorm = velocityNorm, Score = score, CalculatedAt = now,
            //     });
            // }
            // else
            // {
            //     existing.ViewGrowthPct = viewGrowth; existing.VelocityPerHour = velocity;
            //     existing.ViewGrowthNorm = growthNorm; existing.VelocityNorm = velocityNorm;
            //     existing.Score = score; existing.CalculatedAt = now;
            // }
        }
    }
}
```

> Phần truy vấn `VideoMetricSnapshots`/`TrendingScores` để trong comment vì cần quyết trước: dùng thẳng `YTTrendingDbContext` inject vào handler (đơn giản, nhưng lệch convention "mọi entity có repository riêng" của project), hay tạo `IVideoMetricSnapshotRepository`/`ITrendingScoreRepository` cho nhất quán. Chọn 1 hướng rồi bỏ comment + sửa cho khớp lúc code thật — đây là chỗ code mẫu KHÔNG thể tự quyết thay bạn.

✅ Nghiệm thu: chạy job 2 lượt cách nhau → lịch sử snapshot có đủ 2 bản ghi/video, điểm trending tính được sau lượt 2 (lượt 1 chưa đủ điều kiện).

---

## Batch E — BackgroundService + JobsController + README

**Mục đích:** biến 2 command ở Batch C/D thành job chạy nền thật sự theo chu kỳ, cộng thêm cách gọi tay để test/vận hành, và ghi lại cách dùng.

- Tạo background service cho sync job — chạy theo chu kỳ đã cấu hình, tôn trọng cờ bật/tắt, tự bọc lỗi từng lượt chạy để 1 lỗi không làm chết cả job, và chống chạy chồng lượt.
- Tạo background service cho metrics-update job — cấu trúc tương tự, dùng chu kỳ/cờ riêng của nó.
- Tạo endpoint để gọi tay 2 job này ngay lập tức (không cần đợi tới chu kỳ).
- Đăng ký 2 job vào hệ thống hosted service — kiểm tra convention đăng ký lúc code vì project chưa có tiền lệ (Explore không thấy `AddHostedService` nào có sẵn trong repo).
- Viết README: cách bật/tắt job, cách chạy tay, cách xem log.

### Code — `src/YTTrending.Infrastructure/Jobs/SyncChannelJob.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;
using YTTrending.Application.Common.Options;
using YTTrending.Application.Features.Jobs.SyncChannels;

namespace YTTrending.Infrastructure.Jobs;

public sealed class SyncChannelJob(
    IServiceScopeFactory scopeFactory,
    IOptions<TrackingOptions> trackingOptions,
    IOptions<JobOptions> jobOptions,
    ILogger<SyncChannelJob> logger)
    : BackgroundService
{
    private bool _isRunning;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!jobOptions.Value.SyncEnabled)
        {
            logger.LogInformation("SyncChannelJob tắt qua config, không chạy.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(trackingOptions.Value.SyncIntervalHours));

        do
        {
            await RunOnceAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        if (_isRunning)
        {
            logger.LogWarning("SyncChannelJob đang chạy lượt trước, bỏ qua tick này.");
            return;
        }

        _isRunning = true;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new SyncChannelsCommand(), ct);

            if (result.IsSuccess)
                logger.LogInformation("SyncChannelJob chạy xong.");
            else
                logger.LogWarning("SyncChannelJob thất bại: {Error}", result.Error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SyncChannelJob lỗi ngoài dự kiến.");
        }
        finally
        {
            _isRunning = false;
        }
    }
}
```

### Code — `src/YTTrending.Infrastructure/Jobs/MetricsUpdateJob.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MediatR;
using YTTrending.Application.Common.Options;
using YTTrending.Application.Features.Jobs.UpdateVideoMetrics;

namespace YTTrending.Infrastructure.Jobs;

public sealed class MetricsUpdateJob(
    IServiceScopeFactory scopeFactory,
    IOptions<TrackingOptions> trackingOptions,
    IOptions<JobOptions> jobOptions,
    ILogger<MetricsUpdateJob> logger)
    : BackgroundService
{
    private bool _isRunning;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!jobOptions.Value.MetricsUpdateEnabled)
        {
            logger.LogInformation("MetricsUpdateJob tắt qua config, không chạy.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(trackingOptions.Value.MetricsUpdateIntervalHours));

        do
        {
            await RunOnceAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        if (_isRunning)
        {
            logger.LogWarning("MetricsUpdateJob đang chạy lượt trước, bỏ qua tick này.");
            return;
        }

        _isRunning = true;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new UpdateVideoMetricsCommand(), ct);

            if (result.IsSuccess)
                logger.LogInformation("MetricsUpdateJob chạy xong.");
            else
                logger.LogWarning("MetricsUpdateJob thất bại: {Error}", result.Error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MetricsUpdateJob lỗi ngoài dự kiến.");
        }
        finally
        {
            _isRunning = false;
        }
    }
}
```

> `RunOnceAsync` public để `JobsController` gọi tay tái dùng đúng logic (chống chạy chồng dùng chung với vòng lặp `PeriodicTimer`) — tránh viết code chạy-1-lần riêng lặp lại với vòng lặp job.

### Code — `src/YTTrending.Infrastructure/DependencyInjection.cs` (thêm đăng ký hosted service)

```csharp
services.AddHostedService<SyncChannelJob>();
services.AddHostedService<MetricsUpdateJob>();
```

> Kiểm tra: `AddHostedService` cần resolve được chính nó qua DI để `JobsController` gọi `RunOnceAsync` — cách chuẩn là inject `IEnumerable<IHostedService>` rồi `OfType<SyncChannelJob>()`, hoặc đăng ký thêm 1 lần nữa dưới dạng singleton thường (`services.AddSingleton<SyncChannelJob>()` rồi `services.AddHostedService(sp => sp.GetRequiredService<SyncChannelJob>())`) để cùng 1 instance vừa là hosted service vừa gọi tay được. Chọn cách sau khi code — code mẫu `AddHostedService<T>()` đơn thuần ở trên KHÔNG cho phép resolve lại instance đó ở nơi khác.

### Code — `src/YTTrending.API/Controllers/JobsController.cs`

```csharp
using YTTrending.Application.Features.Jobs.SyncChannels;
using YTTrending.Application.Features.Jobs.UpdateVideoMetrics;

namespace YTTrending.API.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(ISender sender) : ControllerBase
{
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken ct)
        => (await sender.Send(new SyncChannelsCommand(), ct)).ToActionResult();

    [HttpPost("metrics-update")]
    public async Task<IActionResult> MetricsUpdate(CancellationToken ct)
        => (await sender.Send(new UpdateVideoMetricsCommand(), ct)).ToActionResult();
}
```

> Bản Controller này gọi thẳng Command qua `ISender`, KHÔNG qua `SyncChannelJob.RunOnceAsync` — đơn giản hơn, bỏ qua cờ chống-chạy-chồng của job. Nếu muốn gọi tay cũng tôn trọng `_isRunning`, đổi Controller sang inject job instance thay vì `ISender` trực tiếp (xem lưu ý DI ở trên).

✅ Nghiệm thu (áp dụng cho cả 2 job): tắt → không thấy log job chạy. Bật → log đúng chu kỳ đã set. Gọi tay qua endpoint → chạy ngay, không cần đợi tick.

---

## Cập nhật docs (đã làm lúc tạo plan này — 01/09/2026)

- `docs/decisions.md` — mục mới *Background job thật*, giải Pending #1 + #2.
- `docs/config.md` — thêm `MetricsUpdateIntervalHours`, `ShortsMaxDurationSeconds`, mục *Jobs & YouTube Client Config*; sửa `MinViewsThreshold` thừa ở Trending Engine Config.
- `ai/current.md` — Mục 7 resume, bỏ note "hoãn có chủ đích", bỏ 2 Pending cũ ở mục Block.
- `ai/setup-base.md`, `ai/setup-base-notes.md` — pointer sang plan này, giữ nguyên checklist gốc để tham khảo.

**Còn lại khi làm từng batch:** cập nhật `ai/current.md` (tick batch xong) + `ai/history.md` (nhật ký) — theo đúng nhịp các Batch 1–6 trước đó (xem tiền lệ ở `ai/plans/batch-4-youtube-client.md` nếu file đó chưa bị xoá).

## Bước cuối

Xoá file này sau khi xong Batch E. Commit từng batch riêng, chỉ add đúng path đã sửa — không `git add -A`.
