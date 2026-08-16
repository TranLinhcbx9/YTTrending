# Current Work

> File này ghi lại đang làm module nào, block ở đâu — cập nhật liên tục trong quá trình dev.

## Tiến độ setup base

Checklist gốc: [`setup-base.md`](setup-base.md) · cách làm từng mục: [`setup-base-notes.md`](setup-base-notes.md)

| Mục | Trạng thái |
|---|---|
| 1. Nền solution | ✅ Xong |
| 2. Domain | ✅ Xong |
| 3. Application — khối dùng chung | 🟡 **Đang làm** — Batch 1+3+2+4+5 /6 xong (`Error`+`Result`, Options, Paging, `IYouTubeClient`, 2 behavior), tiếp theo Batch 6 (`AddApplication()`) |
| 4. Infrastructure — Persistence | ✅ Xong |
| 5. API — wiring | ⬜ |
| 6. Slice nghiệm thu (`AddChannel`) | ⬜ |
| 7. Khung background job | ⬜ |

## Đang làm

- **Đã làm mục 4 trước mục 3** — cố ý: mục 4 chỉ cần đúng 1 thứ từ mục 3 là `IYTTrendingDbContext` (đã làm sớm), còn `Result`/`Behavior`/`Options` thì persistence không dùng tới. Đi vòng này để chốt được schema + migration sớm, vì `UseSnakeCaseNamingConvention()` **buộc phải có trước migration đầu tiên** (A3) — chậm là phải drop DB làm lại.
- **Mục 3 chia 6 batch**, thứ tự chạy đã chốt: **1 → 3 → 2 → 4 → 5 → 6**. Batch 1 đi đầu vì là chỗ duy nhất còn quyết định thiết kế; các batch sau chủ yếu chép từ code mẫu A14/A17/S3.
- **Batch 1 xong (12/08/2026)** — `Common/Models/Error.cs` + `Common/Models/Result.cs` (ban đầu ở `Common/` root, dời vào `Models/` ở Batch 2). 2 câu hỏi treo trước đây (`IResult`? `.Value` khi fail?) **đã chốt**, cùng với quyết định thứ 3 phát sinh khi làm (bỏ implicit conversion) — cả 3 ghi ở [`../docs/decisions.md`](../docs/decisions.md) mục *Application — mục 3, Batch 1*.
- **Batch 3 xong (13/08/2026)** — `Common/Options/{TrackingOptions,TrendingOptions,JobOptions}.cs`, `[Range]` theo [`../docs/config.md`](../docs/config.md). Quyết định phát sinh: `TrendingOptions` không lặp `MinViewsThreshold` (dùng chung của `TrackingOptions`), `JobOptions` chỉ có `Enabled` — ghi ở [`../docs/decisions.md`](../docs/decisions.md) mục *Application — mục 3, Batch 3*. **Chưa đăng ký** `AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` — đó là việc của Batch 6 (`AddApplication()`).
- **Batch 2 xong (15/08/2026)** — `Common/Models/{PagedResult,PagedQuery}.cs` + `Common/Extensions/QueryableExtensions.cs`. Kèm theo là **luật xếp folder** cho `Common/`: type dữ liệu vào `Models/`, folder còn lại theo vai trò, root chỉ giữ `VideoStateRules` — chốt vì S3/A14 đang tự mâu thuẫn. Đã dời `Error.cs`/`Result.cs` sang `Models/` ở commit riêng `cf91181` (git nhận đúng dạng rename), làm được rẻ vì Batch 1 chưa có ai tiêu thụ.
  - 5 quyết định ghi ở [`../docs/decisions.md`](../docs/decisions.md) mục *Application — mục 3, Batch 2*. Đáng nhớ nhất: **bỏ `IOrderedQueryable`** (chặn nhầm code đúng sau `Select`, mà vẫn không chặn được ties) → thứ tự trang giờ là **quy ước**, phải tự nhớ `OrderBy` + `ThenBy(x => x.Id)`.
  - A14 đã viết lại khớp code thật — bản cũ lệch 3 chỗ (`Page` không chặn, `PageSize` reset về 20, `PagedResult` không chặn `PageSize < 1`).
  - ✅ Probe thật (project console tạm ngoài repo, không đụng `Program.cs`): `PageSize` 150→**100**, 0/-5→**20**, `Page` 0/-3→**1**, mặc định `1/20`; `PagedResult` ném cả đường ctor lẫn đường `with { PageSize = 0 }`.
- **Batch 4 xong (16/08/2026)** — `Common/Interfaces/IYouTubeClient.cs` (3 method) + `Common/Models/YouTubeModels.cs` (`ChannelInfo` / `ShortVideoInfo` / `VideoStats`). Không đụng `GlobalUsings.cs`: `.Common.Models` đã mở khoá từ Batch 2, còn `Common.Interfaces` thì `IYTTrendingDbContext` cũng đang `using` tại chỗ — giữ nhất quán.
  - **Chữ ký là TẠM**, S3 ghi sẵn "sửa khi làm Discovery". Chỗ hở đã biết: `GetRecentShortsAsync(channelId, limit, ct)` chỉ phủ vế "N Shorts mới nhất", vế `RecentDays` dựa vào việc `RecentShortsLimit` đủ lớn để phủ hết. Không thêm `publishedAfter` vì `search.list` ghép nó với `maxResults` là **AND** chứ không phải OR — đúng OR phải gọi 2 lần, mà `search.list` tốn **100 đơn vị quota/call**.
  - 7 quyết định ghi ở [`../docs/decisions.md`](../docs/decisions.md) mục *Application — mục 3, Batch 4*. Đáng nhớ nhất: **client không biết config, không lọc ngưỡng** — toàn bộ rule nghiệp vụ (2 vế OR + `MinViewsThreshold`) ở handler, để `FakeYouTubeClient` không phải chép lại luật nào.
  - 🔑 **Cho mục 7 (Metrics Update Job):** `VideoStats` cố tình **không** mang mốc thời gian → job phải tự set `SnapshotAt` bằng `TimeProvider`, **một** `now` duy nhất cho cả lượt sync. Chia lô mà mỗi lô một mốc thì snapshot cùng lượt lệch nhau vài chục giây, Velocity (hiệu 2 snapshot) sai theo.
  - 🔑 **Cũng cho mục 7:** `GetVideoStatsAsync` trả list **có thể ngắn hơn input** (video đã xoá/private vắng mặt) — đối chiếu theo `YoutubeVideoId`, tuyệt đối không theo index.
- **Batch 5 xong (16/08/2026)** — `Common/Behaviors/{LoggingBehavior,ValidationBehavior}.cs`. `dotnet build` → **0 warning / 0 error**. 4 quyết định + phần bất đối xứng constraint ghi ở [`../docs/decisions.md`](../docs/decisions.md) mục *Application — mục 3, Batch 5*.
  - Đáng nhớ nhất: **`ValidationBehavior` ràng `where TResponse : IResult`** — từ .NET 7 DI **lặng lẽ bỏ qua** behavior không thỏa constraint, nên handler nào lỡ trả kiểu không phải `IResult` thì validate không chạy mà **không báo**; quy ước "handler luôn trả `Result`" từ nay là ràng buộc thật.
  - 🔑 **Cho Batch 6:** `AddOpenBehavior(Logging)` phải **trước** `AddOpenBehavior(Validation)` — Logging bọc ngoài mới log được cả validation fail (`FAIL validation.failed`). Đảo thứ tự là mất dòng log đó.
  - 2 comment trong code trỏ `QĐ #2` (`LoggingBehavior`) / `QĐ #4` (`ValidationBehavior`) — chính là số #2/#4 trong mục *Batch 5* của decisions.md, đã giữ số cho khớp.
- Bước tiếp theo: **Batch 6** (`AddApplication()` — đăng ký 2 behavior + `AddValidatorsFromAssembly` + bind cả 3 Options vào đây). `AddInfrastructure()` hiện không cần đụng tới 3 Options này.
- Chi tiết: [`setup-base-notes.md`](setup-base-notes.md) mục S3 + A14/A15/A17.
- `GlobalUsings.cs` của **Application** đã mở khoá `YTTrending.Application.Common` + `.Common.Models` + `.Common.Extensions`; bên **API vẫn để comment** — chờ Batch 6 và mục 5 (`YTTrending.API.Common` chưa tồn tại).
  - ⚠️ **Mìn cho mục 5:** khi API mở khoá `.Common.Models` để viết `ResultExtensions`, `IResult` của mình sẽ đụng `Microsoft.AspNetCore.Http.IResult` (nằm trong implicit usings của Web SDK) → CS0104 ambiguous. Gỡ bằng `global using IResult = YTTrending.Application.Common.Models.IResult;` bên API.

## Nợ verify (chốt ở mục 6)

- **`required` + EF Core 8 materialization** (nợ từ mục 2) — POST qua Swagger → có row → GET đọc lại được.
- **Model binding có ghi được vào `init` accessor không.** Về lý thuyết có (`init` chỉ là setter kèm modreq ở tầng IL, binding đi bằng reflection nên `CanWrite` vẫn `true`) nhưng chưa có đường HTTP nào để chứng minh. Tiêu chí S6 `?pageSize=999999` → cap về 100 là chỗ đóng. Vỡ thì lùi `init` → `set`, mất tính bất biến chứ không mất việc cap.
- **Hình dạng `PagedResult` qua JSON** — `GET ?page=1&pageSize=2` đúng shape đã chốt với FE.
- **Thứ tự trang ổn định** — thêm tiêu chí vào S6: seed ≥4 channel rồi so `?page=1&pageSize=2` với `?page=2&pageSize=2`, **hai trang không được trùng item nào**. Đã bỏ `IOrderedQueryable` nên không có gì chặn lúc compile; đây là chỗ duy nhất bug thứ-tự-bất-định lộ ra trước khi lên Dashboard thật.
- **Chữ ký `IYouTubeClient` có dùng được thật không** (nợ từ Batch 4) — `FakeYouTubeClient` implement được cả 3 method mà không phải sửa interface. Interface thuần thì build luôn xanh, chỉ tới lúc có người implement mới biết thiếu field gì.
- **`ValidationBehavior` trả đúng `Result<T>` fail kèm `fields` camelCase** (nợ từ Batch 5) — reflection dựng `Result<int>.Failure` chỉ đúng/nổ lúc chạy: POST body sai qua Swagger → **400** với `fields` key camelCase (vd `youtubeChannelId`), **không** phải 500. Đóng ở S6.
- **Constraint `where TResponse : IResult` không làm DI bỏ qua behavior** (nợ từ Batch 5) — nếu bị bỏ qua thì validate im lặng không chạy, request lọt xuống handler → **200 thay vì 400**. Cùng đóng ở S6: POST rỗng phải ra 400 chứ không 200.

## Block / Cần quyết định

- Không có gì chặn. Pending #1 (tách snapshot frequency) và #2 (quota YouTube) chỉ ảnh hưởng lúc làm feature thật, không chặn base — xem [`../docs/decisions.md`](../docs/decisions.md).

## Đã chốt

Toàn bộ quyết định setup base đã ghi vào [`../docs/decisions.md`](../docs/decisions.md) mục "Setup base": config từ `appsettings.json`, Postgres local, FE Angular repo riêng, swagger không auto-gen, status VARCHAR, snake_case, Serilog, test hoãn.

## Đã xong

### Trước setup base

- Solution 4 project (Domain / Application / Infrastructure / API) trên .NET 8, Swagger mặc định.
- Chốt kiến trúc: bỏ Repository, MediatR 12.x, BackgroundService cho job, Result pattern, TimeProvider.

### Mục 1 — Nền solution ✅

- [x] **DB local** — **Postgres 18.1 trên Windows**, cài ở `C:\Program Files\PostgreSQL\18`. Binary (`psql`, `pg_dump`) **không có trong PATH** — muốn dùng thì thêm cho phiên hiện tại: `$env:PATH += ";C:\Program Files\PostgreSQL\18\bin"`. User `postgres`, port 5432, auth password.
  - **Không `createdb` tay**: `yttrending_dev` do `dotnet ef database update` tự tạo ở mục 4 (EF nối vào DB `postgres` rồi phát `CREATE DATABASE`). Cần user có quyền `CREATEDB`.
  - `yttrending_test` **chưa tạo** — Phase 1 hoãn test nên chưa cần.
- [x] **`Directory.Packages.props`** — Central Package Management, pin toàn bộ version, `MediatR` khoá cứng `12.4.1`, bật `CentralPackageTransitivePinningEnabled`.
- [x] **`Directory.Build.props`** — `WarningsAsErrors=nullable`, `EnforceCodeStyleInBuild`, gom `TargetFramework` / `Nullable` / `ImplicitUsings` về một chỗ.
- [x] **`.editorconfig`** — file-scoped namespace + `IFoo` ở mức warning, còn lại suggestion; tắt CA2007 và IDE0058.
- [x] **`.gitignore`** — không cần sửa, dòng `[Ll]ogs/` đã phủ `logs/`.
- [x] **Package 4 project** — đúng bảng ở [`setup-base-notes.md`](setup-base-notes.md) S1; `EntityFrameworkCore.Design` để `PrivateAssets=all` cho khỏi rò sang API.
- [x] **Xóa 3 `Class1.cs`**.
- [x] **`GlobalUsings.cs` ×4** — hiện chỉ khai using của package; using namespace nội bộ để comment sẵn (khai trước khi namespace tồn tại là CS0246).

✅ Nghiệm thu: `dotnet build` → 0 warning / 0 error · `psql -d yttrending_dev -c '\conninfo'` → connect được.

### Mục 2 — Domain ✅

- [x] **`Enums/VideoStatus.cs`** — `New` / `Tracking` / `Archived`.
- [x] **`Common/AuditableEntity.cs`** — `CreatedAt` / `UpdatedAt`, `private set` (A4).
  - Đã cân nhắc đổi tên thành `BaseEntity` gom cả `Id` → **bỏ**. Khóa không đồng nhất (`video_metric_snapshots.id` BIGINT, `trending_scores` lấy `video_id` làm PK nên không có cột `id`). Tên `AuditableEntity` còn là bộ lọc của `ChangeTracker.Entries<AuditableEntity>()` — xem [`setup-base-notes.md`](setup-base-notes.md) A20.
- [x] **5 entity** trong `Entities/` — **anemic**: chỉ property `{ get; set; }`, không method / ctor / factory. Tổng 132 dòng cho cả 5 file.
- [x] **2 invariant** ở `Application/Common/VideoStateRules.cs` — `StartTracking(video)` chỉ từ NEW; `Archive(video, now)` chỉ chặn khi đã ARCHIVED (**không** chặn theo trạng thái nguồn, để NEW → ARCHIVED thẳng vẫn chạy), set luôn `ArchivedAt`.
- [x] **Mở khoá global using** `.Common` / `.Entities` / `.Enums` ở Domain / Application / Infrastructure.

**Ba quyết định phát sinh khi làm** (đã sửa docs cho khớp):

1. **Video seed luôn views/likes/comments lúc discovery.** Discovery đã cầm sẵn số liệu từ bước lọc `MinViewsThreshold`, để trống thì video vừa nhận vì có 100k view lại hiện 0 view. [`../docs/database.md`](../docs/database.md) đã sửa dòng `latest_views` + thêm ghi chú.
2. **`SavedIdea` kế thừa `AuditableEntity`** → bảng `saved_ideas` thêm cột `updated_at`. Hợp lý vì `note` sửa được. A4 đã cập nhật.
3. **Đổi entity sang anemic (07/08/2026)** — làm xong theo hướng static factory + private setter rồi mới đổi. Số đo lúc quyết định: cả Domain chỉ có 2 câu `if`, 8/15 method là nghi lễ gán thuần. Dùng `required` thay vai trò "tạo xong là đủ field" của factory; 2 invariant dời sang `VideoStateRules`. **Đánh đổi đã biết:** rule terminal-state từ ràng buộc compiler thành **quy ước** (`video.Status = ...` giờ compile được), và test chuyển trạng thái từ unit test thuần thành test qua handler. Sửa docs: [`../docs/architecture.md`](../docs/architecture.md), [`../docs/domain/video-lifecycle.md`](../docs/domain/video-lifecycle.md), S2.

✅ Nghiệm thu: `dotnet build` → 0 warning / 0 error · grep `Entities/` không còn method/ctor nào · probe object initializer + `VideoStateRules` → compile sạch · probe `new Video { Title = "x" }` → **CS9035** ×9 field bắt buộc, `required` có hiệu lực.

⚠️ **Còn nợ verify:** `required` + EF Core 8 materialization — gom vào mục [Nợ verify](#nợ-verify-chốt-ở-mục-6) ở trên. Về lý thuyết EF ghi qua backing field nên không dính check compile-time. Mục 4 đã chứng minh được **một nửa**: model build + migration sinh đúng, nhưng chưa có đường ghi/đọc thật nào nên **chưa đóng** — dời mốc sang **mục 6 (`AddChannel`)**: POST qua Swagger → có row trong DB → GET đọc lại được. Vỡ thì bỏ `required`, không ảnh hưởng quyết định anemic.

### Mục 4 — Infrastructure / Persistence ✅

- [x] **`IYTTrendingDbContext`** ở `Application/Common/Interfaces/` — 5 `DbSet<>` (chỉ `{ get; }`) + `SaveChangesAsync`. **Không** expose `DatabaseFacade`: đã bỏ `TransactionBehavior` nên không chỗ nào cần, còn `ExecuteUpdateAsync` của Cleanup Job là extension trên `IQueryable<T>` nên vẫn gọi được.
- [x] **`Persistence/YTTrendingDbContext.cs`** — primary constructor `(DbContextOptions, TimeProvider clock)`, `ApplyConfigurationsFromAssembly`, override **cả 2** bản `SaveChangesAsync` và `SaveChanges` để audit không bị lọt.
  - Audit ghi qua `entry.Property(nameof(...)).CurrentValue` vì `CreatedAt`/`UpdatedAt` là `private set` (A4) — đường này ghi thẳng backing field, không cần mở public setter.
  - Lỗ nhỏ đã biết: bản `SaveChangesAsync(bool, CancellationToken)` không override; interface không expose nên handler không gọi tới được.
- [x] **5 `IEntityTypeConfiguration`** trong `Persistence/Configurations/` — `HasMaxLength`/`HasPrecision` khai tay theo [`../docs/database.md`](../docs/database.md); `Description`/`Note` cố tình bỏ trống để thành `text`; status `HasConversion<string>()` + `HasMaxLength(16)` (A11); unique index trên `youtube_channel_id`/`youtube_video_id`/`video_id`; `HasQueryFilter` soft-delete cho `Video`; FK khai tay cho `VideoMetricSnapshot` + `TrendingScore` (A26 mục b).
- [x] **`AddInfrastructure()`** — `AddSingleton(TimeProvider.System)` + `AddDbContext` (`UseNpgsql` + `UseSnakeCaseNamingConvention`) + `AddScoped<IYTTrendingDbContext>` trỏ về cùng instance. **Chưa bind Options** (chờ mục 3).
- [x] **Migration `InitialCreate`** ở `Persistence/Migrations/`, đã apply. Auto-migrate lúc startup qua cờ `Database:AutoMigrate` (`true` ở Development, `false` ở `appsettings.json`) — dùng `MigrateAsync()`, không `EnsureCreated()` (A18).
- [x] **Connection string để thẳng `appsettings.Development.json`**, không dùng user-secrets — ngoại lệ có chủ ý so với A7, xem lý do ở đó.

**Vướng khi làm:**

1. **`dotnet-ef` phải cùng dòng version với runtime.** Tool 7.0.10 + runtime EF 8.0.11 → bị từ chối thẳng (*"tools version '7.0.10' is older than that of the runtime"*). Nâng bằng `dotnet tool update --global dotnet-ef --version 8.*`. Đường PMC thì cần thêm package `Microsoft.EntityFrameworkCore.Tools` vào Infrastructure mới có cmdlet `Add-Migration`.
2. **PMC tự thêm `Microsoft.EntityFrameworkCore.Design` vào API** (startup project bắt buộc phải reference) — nhưng thêm dạng trần, làm rò assembly design-time ra output. Đã khai lại kèm `PrivateAssets=all` cho khớp Infrastructure.
3. **Migration đầu sinh nhầm tên + nhầm chỗ** (`init_db` ở `Infrastructure/Migrations/`) — đã drop DB, xóa file, sinh lại đúng `InitialCreate` ở `Persistence/Migrations/`. Nội dung migration giống hệt bản cũ, chỉ khác tên class + namespace.
4. **Warning `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning`** — do `Video` có query filter còn 3 entity con phụ thuộc bằng FK bắt buộc. Chỉ là log lúc build model, không vỡ build, không cần xử lý.

✅ Nghiệm thu: `dotnet build` → 0 warning / 0 error · migration khớp 7/7 điểm soi so với [`../docs/database.md`](../docs/database.md) (snake_case, `status varchar(16)`, views/likes/comments `bigint`, `trending_scores` PK = `video_id` không có cột `id`, FK của `video_metric_snapshots` + `trending_scores` đều trỏ `videos.id`, `numeric(10,2)`/`(14,2)`/`(5,2)`, không có bảng `app_config`) · `psql \dt` → 5 bảng + `__EFMigrationsHistory` · `dotnet ef migrations has-pending-model-changes` → không lệch model · `dotnet run` → Swagger `HTTP 200` ở `localhost:5118`.
