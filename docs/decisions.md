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
- **Bỏ toàn bộ Repository pattern**: không có `IChannelRepository`/`IVideoRepository`/`IMetricsSnapshotRepository`/`ISavedIdeaRepository`. Command lẫn Query đều dùng `IYTTrendingDbContext` trực tiếp — chỉ có 1 DB duy nhất nên lớp bọc thêm không chặn được lỗi gì. Interface ra ngoài chỉ còn 2: `IYTTrendingDbContext`, `IYouTubeClient`.
- **Background job hosting**: chốt `BackgroundService` + `PeriodicTimer` built-in .NET, không dùng Hangfire/Quartz. Job chỉ đóng vai cái đồng hồ, logic nằm trong Command ở Application.
- **Result pattern thay exception cho lỗi nghiệp vụ**: `Error` mang `ErrorType` (Validation/NotFound/Conflict) để API map HTTP status ở một chỗ duy nhất. Exception chỉ dành cho lỗi hạ tầng.
- **Bỏ `TransactionBehavior`**: một `SaveChangesAsync` đã là một transaction; Phase 1 không có handler nào ghi 2 lần.
- **Dùng `TimeProvider` thay `DateTime.UtcNow`**: gần như mọi rule đều dính thời gian (RecentDays, velocity, retention) — không abstract thì không test được.
- **Test bằng EF Core Sqlite in-memory**, không dùng EF InMemory provider (không enforce constraint, dịch query khác Postgres).

### Setup base (chốt 05/08/2026 — xem [`../ai/setup-base.md`](../ai/setup-base.md))

- **Config đọc từ `appsettings.json` + Options pattern** *(đóng pending #3)*: bind qua `ValidateOnStart`, handler dùng `IOptionsMonitor`. Bảng `app_config` **không tạo** trong migration đầu tiên — để dành Phase 2 khi có UI sửa config runtime. Secrets nằm trong `dotnet user-secrets` — **trừ connection string Development**, xem ngoại lệ ở mục 4 dưới.
- **DB local: Postgres cài sẵn trên máy**, database riêng `yttrending_dev` — không dùng Docker. Database **không tạo tay**: `dotnet ef database update` tự `CREATE DATABASE` nếu chưa có.
- **`videos.status` lưu VARCHAR + `HasConversion<string>()`**, không dùng native Postgres ENUM: tránh phải viết `ALTER TYPE` thủ công mỗi lần thêm trạng thái, và giữ schema tạo được trên Sqlite.
- **Tên bảng/cột `snake_case`** qua `EFCore.NamingConventions` — phải bật trước migration đầu tiên.
- **Logging: Serilog + file sink**, rolling theo ngày, giữ 7 ngày — vì job chạy đêm cần đọc lại log vào sáng hôm sau.
- **Frontend: Angular 20+, để ở repo riêng.** Backend chỉ là API thuần, không host static file, không có project FE trong solution. Kéo theo: API phải bật CORS, và hợp đồng JSON (camelCase, enum trả string, list bọc `PagedResult`, lỗi cùng một hình dạng) phải giữ ổn định vì đổi là sửa cả hai repo.
- **`swagger.json` chỉ dùng để tham chiếu**, không sinh TypeScript client tự động — model bên Angular viết tay. Đổi lại phải kỷ luật: đổi shape DTO ở BE thì tự nhớ sửa interface bên FE, compiler không báo giúp.
- **Test hoãn sang phase sau**: base không tạo test project. Vẫn giữ `IYouTubeClient` và `TimeProvider` vì đó là thiết kế (chạy được `FakeYouTubeClient`, tua được thời gian khi debug), không phải công việc viết test.
- **Audit `created_at`/`updated_at` làm bằng override `SaveChanges` trong `YTTrendingDbContext`**, không dùng `SaveChangesInterceptor`: chỉ có 1 DbContext và 1 mối quan tâm lúc save, interceptor không chặn thêm lỗi nào mà lại khó tìm hơn (cùng lý do đã bỏ Repository). Cắt sang interceptor khi xuất hiện mối quan tâm thứ hai — soft-delete tự động, domain events, outbox.
- **Không đưa vào base**: Repository/Specification, AutoMapper, `BaseController<T>` generic, Unit of Work, caching, API versioning, health check, rate limiting, Docker hóa API.

### Domain — mục 2 (chốt 05/08/2026)

- **`videos` có thêm 3 cột denormalize** `latest_views` / `latest_likes` / `latest_comments` (BIGINT): schema gốc không có, nhưng dashboard filter/sort theo views và `setup-base-notes.md` (A17) đã giả định sẵn cột này. Discovery seed lần đầu (đã cầm sẵn số liệu từ bước lọc `MinViewsThreshold`), sau đó Metrics Update Job ghi đè mỗi lần sync — cả hai đều ghi đè toàn bộ, không cộng dồn, nên không lệch với snapshot mới nhất. [`database.md`](database.md) đã cập nhật.
- **Navigation property: 1 chiều từ `Video`** — `Video.Channel`, `Video.TrendingScore`, `Video.SavedIdea`. Không có `Channel.Videos` hay `Video.Snapshots` (collection ngược), tránh vô tình `Include` cả nghìn dòng snapshot.
- **`VideoStateRules.Archive()` cho phép từ NEW lẫn TRACKING** — chỉ chặn khi đã ARCHIVED (đúng nghĩa chặn terminal-state). Video rớt khỏi recent list trước khi kịp `StartTracking()` vẫn archive được, không kẹt vĩnh viễn ở NEW. [`domain/video-lifecycle.md`](domain/video-lifecycle.md) đã cập nhật.
- **`TrendingScore.Score`, không phải `TrendingScore.TrendingScore`**: class không được có member trùng tên class (CS0542). Cột DB đổi theo: `trending_score` → `score`.
- **`SavedIdea` kế thừa `AuditableEntity`** (chốt lại 07/08/2026, thay cho phương án `IHasCreatedAt`) — bảng `saved_ideas` vì thế có thêm cột `updated_at`. Hợp lý vì `note` sửa được, và `created_at` ở đây đúng nghĩa "row sinh lúc nào" (bookmark = insert, không lệch được) nên để audit điền là chuẩn — khác `snapshot_at`/`calculated_at`.
- **Invariant vi phạm ném `InvalidOperationException`**, không tạo `DomainException` riêng: chuyển trạng thái sai là bug gọi sai thứ tự ở Application, không phải lỗi nghiệp vụ dự kiến được nên không đi qua `Result`. Tách exception riêng khi nào có chỗ cần catch phân biệt.
- **Entity là anemic — chỉ property, không method** (chốt 07/08/2026, sau khi đã làm xong theo hướng ngược lại). Đo lúc quyết định: cả Domain chỉ có 2 câu `if`, 8/15 method là nghi lễ gán thuần quanh `private set`. `required` thay vai trò "tạo xong là đủ field" của static factory; 2 invariant dời sang `Application/Common/VideoStateRules.cs`. **Đánh đổi đã biết:** rule terminal-state từ ràng buộc compiler thành quy ước (`video.Status = ...` compile được), test chuyển trạng thái từ unit test thuần thành test qua handler. Đổi lúc Application còn trống nên không phải sửa call site nào.

### Infrastructure / Persistence — mục 4 (chốt 11/08/2026)

- **Tên `YTTrendingDbContext` / `IYTTrendingDbContext`**, không phải `AppDbContext` / `IAppDbContext` như bản docs trước. Solution chỉ có đúng một DbContext nên "App" không phân biệt được gì; tên mang prefix dự án đọc log/stack trace là biết ngay của ai. Toàn bộ docs đã đổi theo.
- **Connection string Development để thẳng trong `appsettings.Development.json`**, không qua user-secrets — ngoại lệ có chủ ý so với A7 ([`../ai/setup-base-notes.md`](../ai/setup-base-notes.md)). DB là Postgres local, password chỉ có giá trị trên đúng một máy; đổi lấy việc `dotnet ef` / PMC / F5 đọc cùng một chỗ. **YouTube API key vẫn đi user-secrets** — key đó có quota và gắn với Google account.
- **Identity là `GENERATED BY DEFAULT AS IDENTITY`** (mặc định Npgsql), không phải `ALWAYS`: vẫn insert `id` tường minh được khi cần seed data / import lại, `ALWAYS` thì phải `OVERRIDING SYSTEM VALUE`. [`database.md`](database.md) đã sửa.
- **FK khai tay cho `VideoMetricSnapshot` và `TrendingScore`**: hai entity này không có navigation property ở cả hai chiều (hệ quả của quyết định "navigation 1 chiều từ `Video`") nên convention EF **không** tự sinh FK — bỏ qua thì `video_id` chỉ là cột `int` trơn, migration vẫn chạy ngon mà DB không ai chặn. Chi tiết A26.
- **Migration nằm ở `Infrastructure/Persistence/Migrations/`**, tên theo động từ + đối tượng PascalCase (A18) — `InitialCreate`, không phải `init_db`. Sửa sau khi đã apply thì phải drop DB làm lại vì `migration_id` đã ghi vào `__EFMigrationsHistory`.
- **`Microsoft.EntityFrameworkCore.Design` ở API khai kèm `PrivateAssets=all`**: startup project bắt buộc phải reference package này cho `dotnet ef`/PMC hoạt động, nhưng đây là dependency design-time — để trần là rò assembly ra output.
- **Chưa bind Options trong `AddInfrastructure()`** — nợ lại tới khi mục 3 tạo xong `TrackingOptions`/`TrendingOptions`/`JobOptions`.

### Application — mục 3, Batch 1 (chốt 11-12/08/2026)

- **`Result` và `Result<T>` chia sẻ `IResult { IsSuccess, Error }`**: `LoggingBehavior<TRequest,TResponse>` bọc mọi handler nên `TResponse` lúc là `Result`, lúc là `Result<int>` — không có interface chung thì `response is Result<T>` không viết được (CS0246, không `T` nào trong scope), buộc phải reflection (`GetProperty("IsSuccess")`) vốn chậm và đổi tên property thì compiler im lặng. **Giá:** 1 interface 5 dòng. **Mua được:** `is IResult { IsSuccess: false }` một dòng, và `ResultExtensions` (mục 5) tách được `MapError(IResult)` dùng chung thay vì copy bảng map HTTP status ra 2 lần.
- **`.Value` khi `IsSuccess == false` ném `InvalidOperationException`**, không trả `default(T)` (phương án bị loại — chính là sketch cũ ở [`architecture.md`](architecture.md)). Hai lý do: (1) `default` là giá trị *hợp lệ* của kiểu chứ không phải "rỗng" — `Result<int>` fail vẫn ra `0`, quên `if (IsSuccess)` thì API trả `200 OK { channelId: 0 }` không exception không log; (2) `WarningsAsErrors=nullable` đang bật, chọn `default` thì `Value` phải khai `T?` mà compiler không suy được `IsSuccess == true ⇒ Value != null`, nên code đúng logic vẫn gãy build, phải rải `!` khắp handler — nghịch lý là phương án "an toàn vì không throw" lại bắt dùng đúng toán tử tắt kiểm tra null. Tiền lệ BCL: `Nullable<T>.Value` cũng ném `InvalidOperationException`. Khớp ranh giới "bug gọi sai → exception" đã chốt ở mục Kiến trúc trên.
- **Không dùng implicit conversion** `T → Result<T>` / `Error → Result<T>` dù A15 ([`../ai/setup-base-notes.md`](../ai/setup-base-notes.md)) gợi ý: implicit không sai về type-safety (vẫn compile-time checked) nhưng `return channel.Id;` không có chữ nào cho biết đang tạo `Result<int>` — phải biết trước type mới đọc hiểu, và text-search không tìm ra chỗ construct vì không có `Success(`/`Failure(` trên dòng đó. **Giá:** mọi handler gõ dài hơn, luôn phải viết `Result<int>.Success(...)`/`Failure(...)` tường minh. **Lợi ích phụ:** hết rủi ro CS0456 — 2 operator generic `implicit operator Result<T>(T)` và `implicit operator Result<T>(Error)` đụng nhau khi đóng kiểu `T = Error`.

### Application — mục 3, Batch 3 (chốt 13/08/2026)

- **`TrendingOptions` không có field `MinViewsThreshold`** dù [`config.md`](config.md) liệt kê nó ở cả 2 khối JSON (Tracking lẫn Trending). Công thức thật ở [`domain/trending-engine.md`](domain/trending-engine.md) chỉ dùng `ViewGrowthWeight`/`VelocityWeight`; `MinViewsThreshold` chỉ có một chỗ dùng thật là filter Discovery. Giữ 1 nguồn duy nhất ở `TrackingOptions` để tránh 2 Options lệch giá trị nhau khi tune config — trùng lặp JSON trong `config.md` không phản ánh 2 field riêng.
- **`JobOptions` chỉ có `Enabled`**, không thêm `RunOnStartup` dù `setup-base-notes.md` A21 có gợi ý cờ "chạy ngay lúc start" cho `PeriodicTimer`. Job thật (mục 7) chưa làm — thêm field bây giờ là đoán trước yêu cầu chưa có, thêm khi làm `SyncChannelJob`.

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
