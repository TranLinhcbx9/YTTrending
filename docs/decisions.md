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

### Setup base (chốt 05/08/2026 — xem [`../ai/setup-base.md`](../ai/setup-base.md))

- **Config đọc từ `appsettings.json` + Options pattern** *(đóng pending #3)*: bind qua `ValidateOnStart`, handler dùng `IOptionsMonitor`. Bảng `app_config` **không tạo** trong migration đầu tiên — để dành Phase 2 khi có UI sửa config runtime. Secrets (connection string, YouTube API key) nằm trong `dotnet user-secrets`.
- **DB local: Postgres cài sẵn trên máy**, database riêng `yttrending_dev` — không dùng Docker.
- **`videos.status` lưu VARCHAR + `HasConversion<string>()`**, không dùng native Postgres ENUM: tránh phải viết `ALTER TYPE` thủ công mỗi lần thêm trạng thái, và giữ schema tạo được trên Sqlite.
- **Tên bảng/cột `snake_case`** qua `EFCore.NamingConventions` — phải bật trước migration đầu tiên.
- **Logging: Serilog + file sink**, rolling theo ngày, giữ 7 ngày — vì job chạy đêm cần đọc lại log vào sáng hôm sau.
- **Frontend: Angular 20+, để ở repo riêng.** Backend chỉ là API thuần, không host static file, không có project FE trong solution. Kéo theo: API phải bật CORS, và hợp đồng JSON (camelCase, enum trả string, list bọc `PagedResult`, lỗi cùng một hình dạng) phải giữ ổn định vì đổi là sửa cả hai repo.
- **`swagger.json` chỉ dùng để tham chiếu**, không sinh TypeScript client tự động — model bên Angular viết tay. Đổi lại phải kỷ luật: đổi shape DTO ở BE thì tự nhớ sửa interface bên FE, compiler không báo giúp.
- **Test hoãn sang phase sau**: base không tạo test project. Vẫn giữ `IYouTubeClient` và `TimeProvider` vì đó là thiết kế (chạy được `FakeYouTubeClient`, tua được thời gian khi debug), không phải công việc viết test.
- **Audit `created_at`/`updated_at` làm bằng override `SaveChanges` trong `AppDbContext`**, không dùng `SaveChangesInterceptor`: chỉ có 1 DbContext và 1 mối quan tâm lúc save, interceptor không chặn thêm lỗi nào mà lại khó tìm hơn (cùng lý do đã bỏ Repository). Cắt sang interceptor khi xuất hiện mối quan tâm thứ hai — soft-delete tự động, domain events, outbox.
- **Không đưa vào base**: Repository/Specification, AutoMapper, `BaseController<T>` generic, Unit of Work, caching, API versioning, health check, rate limiting, Docker hóa API.

### Domain — mục 2 (chốt 05/08/2026)

- **`videos` có thêm 3 cột denormalize** `latest_views` / `latest_likes` / `latest_comments` (BIGINT): schema gốc không có, nhưng dashboard filter/sort theo views và `setup-base-notes.md` (A17) đã giả định sẵn cột này. Metrics Update Job ghi đè mỗi lần sync — chỉ 1 writer nên không sợ lệch với snapshot mới nhất. [`database.md`](database.md) đã cập nhật.
- **Navigation property: 1 chiều từ `Video`** — `Video.Channel`, `Video.TrendingScore`, `Video.SavedIdea`. Không có `Channel.Videos` hay `Video.Snapshots` (collection ngược), tránh vô tình `Include` cả nghìn dòng snapshot.
- **`Video.Archive()` cho phép từ NEW lẫn TRACKING** — chỉ chặn khi đã ARCHIVED (đúng nghĩa chặn terminal-state). Video rớt khỏi recent list trước khi kịp `StartTracking()` vẫn archive được, không kẹt vĩnh viễn ở NEW. [`domain/video-lifecycle.md`](domain/video-lifecycle.md) đã cập nhật.
- **`TrendingScore.Score`, không phải `TrendingScore.TrendingScore`**: class không được có member trùng tên class (CS0542). Cột DB đổi theo: `trending_score` → `score`.
- **`SavedIdea.CreatedAt` set trong static factory**, không tạo interface `IHasCreatedAt` riêng — chỉ một chỗ duy nhất tạo ra `SavedIdea`, không sợ quên.
- **Invariant vi phạm ném `InvalidOperationException`**, không tạo `DomainException` riêng: chuyển trạng thái sai là bug gọi sai thứ tự ở Application, không phải lỗi nghiệp vụ dự kiến được nên không đi qua `Result`. Tách exception riêng khi nào có chỗ cần catch phân biệt.

## Pending (chưa chốt)

### 1. Snapshot frequency tách riêng khỏi Sync Interval?

Hiện tại gộp chung: sync channel mỗi `SyncIntervalHours` = tạo snapshot mỗi `SyncIntervalHours`.

Câu hỏi mở: có cần tách riêng — ví dụ sync channel (detect video mới) mỗi 6h, nhưng snapshot metrics mỗi 1h riêng cho video đang hot (đang TRACKING) để tính Trending Score chính xác hơn?

Không ảnh hưởng schema — nếu tách sau này chỉ cần thêm 1 job riêng, bảng snapshot không đổi.

→ Ảnh hưởng: [`config.md`](config.md), [`domain/background-jobs.md`](domain/background-jobs.md), [`domain/metrics-snapshot.md`](domain/metrics-snapshot.md).

### 2. API Quota (YouTube Data API)

Với 20–50 channel, sync mỗi vài giờ, cần kiểm tra thực tế quota YouTube Data API có đủ dùng không trước khi lên production.

→ Ảnh hưởng: [`domain/background-jobs.md`](domain/background-jobs.md), [`domain/channel-management.md`](domain/channel-management.md).

### ~~3. Config đọc từ đâu — `appsettings.json` hay bảng `app_config`?~~ ✅ ĐÃ CHỐT

Chốt `appsettings.json` + Options pattern — xem mục "Setup base" ở trên. [`config.md`](config.md) và [`database.md`](database.md) đã sửa cho khớp.
