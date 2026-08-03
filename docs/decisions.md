# Decisions & Pending Items

## Đã chốt

- **Snapshot = Sync**: Snapshot metrics được tạo mỗi lần Sync Job chạy. `SyncIntervalHours` chính là snapshot frequency — không phải 2 config riêng biệt (mặc định hiện tại là gộp chung).
- **VideoId là khóa duy nhất**: dùng để so sánh/duplicate-check, không dùng title/thumbnail vì các trường này có thể bị chỉnh sửa.
- **ARCHIVED là trạng thái cuối**: không có đường quay lại TRACKING, kể cả khi video tăng trưởng đột biến sau đó.
- **Video chưa đạt `MinViewsThreshold`**: không lưu bất kỳ record nào; nếu sau đạt ngưỡng và vẫn còn trong recent list thì được bắt lại từ đầu, không có snapshot lịch sử trước đó.
- **Trending Score**: chỉ tính theo View Growth + Velocity, không tính engagement (likes/comments), không tính age của video.
- **Normalize min/max**: tính lại mỗi lần Metrics Update Job chạy, không cache — vì tập video đang track thay đổi liên tục.
- **ARCHIVED terminal-state**: rule "không quay lại TRACKING" được enforce ở domain/application layer, không dùng DB trigger.
- **Trending Score storage**: lưu 1 row/video (UPSERT mỗi lần Metrics Update Job chạy), không giữ lịch sử theo thời gian — cần xem biến thiên thì tính lại từ metrics snapshot.
- **Archived retention**: video ARCHIVED quá `ArchivedRetentionDays` (mặc định 30 ngày) được soft-delete bởi Cleanup Job; snapshot liên quan vẫn giữ nguyên.
- **Saved Ideas**: bỏ Tag, bỏ bookmark Channel — chỉ bookmark Video, 1 video/1 bookmark (khóa duy nhất).

### Kiến trúc (xem [`architecture.md`](architecture.md))

- **Runtime .NET 8 (LTS)**: giữ .NET 8 cho Phase 1 dù hết support 10/11/2026 — nâng lên .NET 10 tính ở Phase 2, không phải việc chặn Phase 1.
- **MediatR dừng ở dòng 12.x**: MediatR 13+ đã chuyển sang license thương mại. Dùng 12.x (Apache-2.0), không nâng major.
- **Bỏ toàn bộ Repository pattern**: không có `IChannelRepository`/`IVideoRepository`/`IMetricsSnapshotRepository`/`ISavedIdeaRepository`. Command lẫn Query đều dùng `IAppDbContext` trực tiếp — chỉ có 1 DB duy nhất nên lớp bọc thêm không chặn được lỗi gì. Interface ra ngoài chỉ còn 2: `IAppDbContext`, `IYouTubeClient`.
- **Background job hosting**: chốt `BackgroundService` + `PeriodicTimer` built-in .NET, không dùng Hangfire/Quartz. Job chỉ đóng vai cái đồng hồ, logic nằm trong Command ở Application.
- **Result pattern thay exception cho lỗi nghiệp vụ**: `Error` mang `ErrorType` (Validation/NotFound/Conflict) để API map HTTP status ở một chỗ duy nhất. Exception chỉ dành cho lỗi hạ tầng.
- **Bỏ `TransactionBehavior`**: một `SaveChangesAsync` đã là một transaction; Phase 1 không có handler nào ghi 2 lần.
- **Dùng `TimeProvider` thay `DateTime.UtcNow`**: gần như mọi rule đều dính thời gian (RecentDays, velocity, retention) — không abstract thì không test được.
- **Test bằng EF Core Sqlite in-memory**, không dùng EF InMemory provider (không enforce constraint, dịch query khác Postgres).

## Pending (chưa chốt)

### 1. Snapshot frequency tách riêng khỏi Sync Interval?

Hiện tại gộp chung: sync channel mỗi `SyncIntervalHours` = tạo snapshot mỗi `SyncIntervalHours`.

Câu hỏi mở: có cần tách riêng — ví dụ sync channel (detect video mới) mỗi 6h, nhưng snapshot metrics mỗi 1h riêng cho video đang hot (đang TRACKING) để tính Trending Score chính xác hơn?

Không ảnh hưởng schema — nếu tách sau này chỉ cần thêm 1 job riêng, bảng snapshot không đổi.

→ Ảnh hưởng: [`config.md`](config.md), [`domain/background-jobs.md`](domain/background-jobs.md), [`domain/metrics-snapshot.md`](domain/metrics-snapshot.md).

### 2. API Quota (YouTube Data API)

Với 20–50 channel, sync mỗi vài giờ, cần kiểm tra thực tế quota YouTube Data API có đủ dùng không trước khi lên production.

→ Ảnh hưởng: [`domain/background-jobs.md`](domain/background-jobs.md), [`domain/channel-management.md`](domain/channel-management.md).

### 3. Config đọc từ đâu — `appsettings.json` hay bảng `app_config`?

Hai tài liệu đang mô tả khác nhau:
- [`config.md`](config.md) và bảng `app_config` trong [`database.md`](database.md): config lưu **key-value trong DB**.
- [`architecture.md`](architecture.md): config đọc từ **`appsettings.json`** qua Options pattern + `ValidateOnStart`.

**Đề xuất:** Phase 1 dùng `appsettings.json` + `IOptionsMonitor` (sửa file là ăn ngay, không cần restart, không cần code gì thêm). Bảng `app_config` giữ nguyên trong schema nhưng **chưa dùng** — chỉ cần thiết khi có màn hình UI cho phép sửa config lúc runtime (Phase 2).

Nếu chốt theo đề xuất này thì phải sửa lại phần "Config Principle" ở [`config.md`](config.md) cho khớp.

→ Ảnh hưởng: [`config.md`](config.md), [`database.md`](database.md), [`architecture.md`](architecture.md).
