# Mục 3 — Batch 4: `IYouTubeClient`

> ⚠️ **FILE TẠM — không commit, xoá sau khi xong batch.**
> Bản gốc: `C:\Users\Linh\.claude\plans\tiep-theo-den-lam-witty-russell.md`

## Context

Plan mode ghi file vào `C:\Users\Linh\.claude\plans\` với tên tự sinh, **ngoài repo** → không có trong git, không thấy trong VS Code. Các batch trước: Batch 1 = `roi-gio-minh-thao-jolly-glade.md`, Batch 2 = `check-xem-gio-den-humming-lynx.md`, plan gốc cả mục 3 = `roi-tiep-theo-den-lovely-kahn.md`.

**Vị trí hiện tại:** mục 3 đã xong Batch 1 (`Error`/`Result`), 3 (Options ×3), 2 (Paging). Batch 4 là interface `IYouTubeClient` — 1 trong 2 interface duy nhất dự án expose ra ngoài. Batch 5 (behaviors) + Batch 6 (`AddApplication()`) **không thuộc plan này**, chốt sau.

Batch 4 chỉ tạo 2 file, không có logic chạy được. Nghiệm thu thật nằm ở mục 6 (`FakeYouTubeClient` + `AddChannel`).

---

## File 1 — `src/YTTrending.Application/Common/Models/YouTubeModels.cs`

Vào `Models/` theo luật xếp folder chốt ở Batch 2 (type dữ liệu → `Models/`). Gộp 1 file vì cùng một hợp đồng — đúng tiền lệ `Result.cs` (`IResult`+`Result`+`Result<T>`) và `Error.cs` (`ErrorType`+`Error`).

```csharp
public record ChannelInfo(string YoutubeChannelId, string Name, string Url);

public record ShortVideoInfo(
    string YoutubeVideoId, string Title, DateTimeOffset PublishedAt, int DurationSeconds,
    string? Description, string? ThumbnailUrl, long Views, long Likes, long Comments);

public record VideoStats(string YoutubeVideoId, long Views, long Likes, long Comments);
```

## File 2 — `src/YTTrending.Application/Common/Interfaces/IYouTubeClient.cs`

```csharp
Task<ChannelInfo?>                  GetChannelAsync(string youtubeChannelId, CancellationToken ct);
Task<IReadOnlyList<ShortVideoInfo>> GetRecentShortsAsync(string youtubeChannelId, int limit, CancellationToken ct);
Task<IReadOnlyList<VideoStats>>     GetVideoStatsAsync(IReadOnlyList<string> youtubeVideoIds, CancellationToken ct);
```

**Không đụng `GlobalUsings.cs`**: `.Common.Models` đã mở khoá sẵn; `Common.Interfaces` thì `IYTTrendingDbContext` hiện cũng không có global using — giữ nguyên cho nhất quán.

---

## Cập nhật docs

- **`docs/decisions.md`** — mục mới *Application — mục 3, Batch 4*, ghi kèm phương án bị loại (7 quyết định).
- **`ai/setup-base.md`** — tick dòng `IYouTubeClient`.
- **`ai/current.md`** — bảng tiến độ → Batch 1+3+2+4 xong, tiếp theo Batch 5; đoạn *Batch 4 xong (16/08/2026)*; ghi 1 dòng cho mục 7 về `SnapshotAt`.
- **`ai/setup-base-notes.md`** — S3 tick dòng `IYouTubeClient`, sửa mô tả cho khớp chữ ký thật.

---

## Nghiệm thu

1. `dotnet build` → 0 warning / 0 error.
2. Application vẫn không kéo Npgsql.
3. Soi tay: DTO phủ đủ field `required` của entity —

   | Entity | Field `required` | DTO phủ bằng |
   |---|---|---|
   | `Channel` | `YoutubeChannelId`, `Name`, `Url` | `ChannelInfo` — 3/3 |
   | `Video` | `YoutubeVideoId`, `Title`, `PublishedAt`, `DurationSeconds` | `ShortVideoInfo` — 4/4 (`ChannelId` là int nội bộ) |
   | `VideoMetricSnapshot` | `Views`, `Likes`, `Comments` | `VideoStats` — 3/3 (`VideoId` nội bộ, `SnapshotAt` do job set) |

**Nợ verify sang mục 6:** `FakeYouTubeClient` implement được cả 3 method mà không phải sửa interface.

---

## Bước cuối

Xoá file này (và folder `ai/plans/` nếu rỗng). Commit **chỉ** các path đã sửa, không `git add -A`.

Xong Batch 4 → **Batch 5** (`LoggingBehavior` + `ValidationBehavior`), chốt thiết kế riêng ở lượt sau.
