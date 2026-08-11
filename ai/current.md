# Current Work

> File này ghi lại đang làm module nào, block ở đâu — cập nhật liên tục trong quá trình dev.

## Tiến độ setup base

Checklist gốc: [`setup-base.md`](setup-base.md) · cách làm từng mục: [`setup-base-notes.md`](setup-base-notes.md)

| Mục | Trạng thái |
|---|---|
| 1. Nền solution | ✅ Xong |
| 2. Domain | ✅ Xong |
| 3. Application — khối dùng chung | 🟡 **Tiếp theo** — mới có `IYTTrendingDbContext` + `VideoStateRules` |
| 4. Infrastructure — Persistence | ✅ Xong |
| 5. API — wiring | ⬜ |
| 6. Slice nghiệm thu (`AddChannel`) | ⬜ |
| 7. Khung background job | ⬜ |

## Đang làm

- **Đã làm mục 4 trước mục 3** — cố ý: mục 4 chỉ cần đúng 1 thứ từ mục 3 là `IYTTrendingDbContext` (đã làm sớm), còn `Result`/`Behavior`/`Options` thì persistence không dùng tới. Đi vòng này để chốt được schema + migration sớm, vì `UseSnakeCaseNamingConvention()` **buộc phải có trước migration đầu tiên** (A3) — chậm là phải drop DB làm lại.
- Bước tiếp theo: **mục 3 — phần còn lại**: `Result`/`Error`, `PagedResult`/`PagedQuery`, `QueryableExtensions`, `IYouTubeClient`, 2 behavior, 3 Options, `AddApplication()`. Xong Options thì quay lại `AddInfrastructure()` bind vào.
- Chi tiết: [`setup-base-notes.md`](setup-base-notes.md) mục S3 + A14/A15/A17.
- Khi tạo xong `YTTrending.Application.Common` → mở khoá dòng comment còn lại trong `GlobalUsings.cs` của Application và API.
- 2 câu hỏi treo cần chốt trước khi viết `Result`: (1) `Result` và `Result<T>` có chia sẻ `IResult { IsSuccess, Error }` không (ảnh hưởng `LoggingBehavior`/`ResultExtensions` viết 1 lần hay 2 lần)? (2) `.Value` khi `IsSuccess == false` thì trả `default` hay throw (`Result<int>` fail trả `0` — trùng Id hợp lệ)?

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

⚠️ **Còn nợ verify:** `required` + EF Core 8 materialization. Về lý thuyết EF ghi qua backing field nên không dính check compile-time. Mục 4 đã chứng minh được **một nửa**: model build + migration sinh đúng, nhưng chưa có đường ghi/đọc thật nào nên **chưa đóng** — dời mốc sang **mục 6 (`AddChannel`)**: POST qua Swagger → có row trong DB → GET đọc lại được. Vỡ thì bỏ `required`, không ảnh hưởng quyết định anemic.

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
