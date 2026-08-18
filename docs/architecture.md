# Architecture

## Mục tiêu

Mô tả kiến trúc kỹ thuật: layer nào chịu trách nhiệm gì, phụ thuộc theo chiều nào, và những gì **cố tình không làm** cho gọn. Business rule (discovery, trending, lifecycle...) xem [`domain/`](domain/).

Nguyên tắc xuyên suốt: **Clean Architecture + CQRS ở mức tối thiểu đủ dùng**. Mỗi lớp trừu tượng phải trả lời được "nó chặn được lỗi gì?" — không trả lời được thì bỏ.

> **Doc này mô tả cấu trúc, không chứa code.** Code thật ở [`../src/`](../src/); rule tra nhanh ở [`coding-convention.md`](coding-convention.md); "vì sao chọn/bỏ" ở [`decisions.md`](decisions.md).

## Tech Stack

| Thành phần | Lựa chọn | Ghi chú |
|---|---|---|
| Runtime | **.NET 8 (LTS)** | Hết support 10/11/2026 — Phase 2 tính chuyện lên .NET 10 |
| API | ASP.NET Core Web API + Controllers | Đã có sẵn `Program.cs` + Swagger |
| CQRS | **MediatR 12.x** | Dùng dòng 12 (Apache-2.0, miễn phí). MediatR 13+ đã chuyển license thương mại — **không** nâng lên 13 |
| Validation | FluentValidation 11.x | Chạy qua `ValidationBehavior` |
| ORM | EF Core 8 + Npgsql | Schema xem [`database.md`](database.md) (kiểu dữ liệu đang theo Postgres) |
| HTTP resilience | `Microsoft.Extensions.Http.Resilience` | 1 dòng `.AddStandardResilienceHandler()` cho YouTube client |
| Test | xUnit + FluentAssertions + EF Core Sqlite (in-memory) | **Không** dùng EF InMemory provider |

## Layer Structure

```
YTTrending.API             → Composition root: Controllers, DI wiring, appsettings, Swagger
      ↓
YTTrending.Infrastructure  → EF Core DbContext, YouTube API client, Background Jobs
      ↓
YTTrending.Application     → Command/Query handler, interface (IYTTrendingDbContext, IYouTubeClient), Options
      ↓
YTTrending.Domain          → Entities, Enums, invariant — không phụ thuộc project nào khác
```

### Quy tắc phụ thuộc

| Project | Được reference | NuGet được phép | Cấm |
|---|---|---|---|
| **Domain** | (không) | (không) | EF Core, MediatR, ASP.NET |
| **Application** | Domain | MediatR, FluentValidation, `Microsoft.EntityFrameworkCore`, `Options`/`Logging.Abstractions` | Npgsql (provider), ASP.NET |
| **Infrastructure** | Application | EF Core + Npgsql, Http.Resilience, Hosting | (không cấm gì đặc biệt) |
| **API** | Application **+ Infrastructure** | ASP.NET, Swashbuckle | `using` DbContext / entity EF trong Controller |

> **Vì sao API vẫn reference Infrastructure?** Vì API là composition root — không thể gọi `services.AddInfrastructure()` mà không "thấy" project đó. Ranh giới thật nằm ở chỗ: **Controller chỉ được biết `ISender` và DTO**, tuyệt đối không chạm `YTTrendingDbContext`. Đây là quy ước, compiler không chặn được.

> **Application vẫn phụ thuộc EF Core — và điều đó ổn.** `IYTTrendingDbContext` expose `DbSet<T>`, còn `FirstOrDefaultAsync`/`AsNoTracking` là extension của EF. Nói "Application không biết EF" là tự lừa mình. Ranh giới thật là **provider**: đổi Postgres → SQL Server chỉ động vào Infrastructure.

## Cấu trúc thư mục

```
src/
├── YTTrending.Domain/
│   ├── Entities/           Channel, Video, VideoMetricSnapshot, TrendingScore, SavedIdea
│   └── Enums/              VideoStatus (New/Tracking/Archived)
│
├── YTTrending.Application/
│   ├── Common/
│   │   ├── Interfaces/     IYTTrendingDbContext, IYouTubeClient
│   │   ├── Behaviors/      LoggingBehavior, ValidationBehavior
│   │   ├── Options/        TrackingOptions, TrendingOptions
│   │   └── Result.cs, Error.cs
│   └── Features/
│       ├── Channels/       Commands/ (AddChannel, ToggleChannel) + Queries/
│       ├── Videos/         Queries/ (GetDashboard, GetVideoDetail)
│       ├── SavedIdeas/     Commands/ + Queries/
│       └── Jobs/           SyncChannelsCommand, UpdateMetricsCommand, CleanupArchivedCommand
│
├── YTTrending.Infrastructure/
│   ├── Persistence/
│   │   ├── YTTrendingDbContext.cs
│   │   ├── Configurations/     IEntityTypeConfiguration<T>
│   │   └── Migrations/
│   ├── YouTube/                YouTubeClient (typed HttpClient)
│   ├── BackgroundJobs/         SyncChannelJob, MetricsUpdateJob, CleanupJob
│   └── DependencyInjection.cs
│
└── YTTrending.API/
    ├── Controllers/            ChannelsController, VideosController, SavedIdeasController
    ├── Common/                 ResultExtensions, GlobalExceptionHandler
    └── Program.cs

tests/
└── YTTrending.Application.Tests/     (1 project là đủ cho Phase 1)
```

Một feature = một folder, chứa Command/Query + Handler + Validator cạnh nhau. Sửa tính năng chỉ mở đúng 1 folder.

## Trách nhiệm từng project

### YTTrending.Domain
- Entity thuần + enum. Không attribute EF, không interface repository.
- **Anemic model — entity chỉ chứa property, không method.** Property `{ get; set; }` công khai, tạo bằng object initializer, không static factory.
  - Field bắt buộc dùng `required` để compiler chặn lúc khởi tạo (CS9035) — thay cho vai trò "tạo xong là đủ field" của factory.
  - Field có default nghiệp vụ phải khai tường minh: `Channel.IsEnabled = true`, `Video.Status = VideoStatus.New`. Bỏ sót `IsEnabled` là channel vừa add đã tắt tracking mà không báo lỗi.
- **Invariant KHÔNG nằm ở đây** — chuyển trạng thái video đi qua `Application/Common/VideoStateRules.cs` (rule terminal-state ở [`domain/video-lifecycle.md`](domain/video-lifecycle.md)).
  - Đây là **quy ước, không phải ràng buộc compiler**: `video.Status = ...` vẫn compile được. Đánh đổi đã biết khi chọn anemic — xem [`decisions.md`](decisions.md) (Domain — mục 2).

### YTTrending.Application
- Command/Query handler theo từng domain doc:
  - Discovery Engine — [`domain/discovery-engine.md`](domain/discovery-engine.md)
  - Trending Engine — [`domain/trending-engine.md`](domain/trending-engine.md)
  - Job orchestration — [`domain/background-jobs.md`](domain/background-jobs.md)
- Khai báo interface ra ngoài: **chỉ 2 cái** — `IYTTrendingDbContext`, `IYouTubeClient`.
- Bind + validate Options — xem [`config.md`](config.md).

### YTTrending.Infrastructure
- `YTTrendingDbContext : DbContext, IYTTrendingDbContext` + Fluent API configurations + migrations.
- `YouTubeClient` (typed `HttpClient` + resilience handler).
- 3 `BackgroundService` cho Sync / Metrics / Cleanup.
- `DependencyInjection.cs`: một extension `AddInfrastructure(config)` gom toàn bộ đăng ký.

### YTTrending.API
- `Program.cs`: `AddApplication()` + `AddInfrastructure()` + Swagger + `IExceptionHandler`.
- Controller mỏng: nhận request → `ISender.Send()` → `result.ToActionResult()`. Không if/else nghiệp vụ.

## CQRS với MediatR

### Command (luồng ghi) — qua Domain

Check nghiệp vụ → gọi `IYouTubeClient`/`IYTTrendingDbContext` → đổi trạng thái qua `VideoStateRules` → `SaveChangesAsync` → trả `Result<T>`. Ví dụ `AddChannelCommand`: trùng channel → `Error.Conflict`, không tìm thấy → `Error.NotFound`, ok → `Result<int>.Success(channel.Id)`.

### Query (luồng đọc) — thẳng DbContext, không Repository

`db.Videos.AsNoTracking().WhereIf(...).Select(v => new XxxDto(...)).ToListAsync(ct)` — projection thẳng vào DTO, không load entity thừa, không Repository.

→ Rule handler đầy đủ: [`coding-convention.md`](coding-convention.md) mục 5. Code slice đầu tiên sẽ ở `src/YTTrending.Application/Features/` (mục 6 — [`../ai/setup-base.md`](../ai/setup-base.md)).

### Vì sao KHÔNG có Repository

Bản trước của doc này khai `IChannelRepository`, `IVideoRepository`, `IMetricsSnapshotRepository`, `ISavedIdeaRepository` — **đã bỏ hết**. Lý do:

- Repository tồn tại để đổi được nguồn dữ liệu. Ở đây chỉ có **một** DB duy nhất, vĩnh viễn.
- `DbSet<T>` + `IQueryable` đã là Repository + Unit of Work rồi. Bọc thêm một lớp chỉ để `return _db.Videos.Where(...)` là gián tiếp thừa.
- Query cần shape linh hoạt (dashboard nhiều filter, chi tiết video kèm chart) — bọc Repository sẽ đẻ ra `GetByChannelAndScoreAndDateRange...` vô tận.

`IYouTubeClient` thì **giữ**, vì nó thật sự chặn được thứ có ích: test handler không cần gọi mạng thật.

## Result Pattern

Lỗi nghiệp vụ dự kiến được (duplicate channel, không tìm thấy video) → trả `Result`. Exception chỉ dành cho lỗi hạ tầng bất thường (mất kết nối DB, bug).

Ba kiểu: `Result` (command không trả gì — tránh `Result<bool>`), `Result<T>` (có giá trị), `Error(Code, ErrorType, Message)` + `Fields?` (lỗi nhiều field từ FluentValidation); cả hai `Result` chia chung `IResult` để `LoggingBehavior`/`ResultExtensions` xử lý bằng một nhánh. Constructor `private` — chỉ vào qua `Success()`/`Failure()`, không dựng được trạng thái vô lý (`IsSuccess = true` mà vẫn có `Error`). `.Value` khi đã fail → **ném** `InvalidOperationException`, không trả `default`. **Không** implicit conversion `T → Result<T>` — handler luôn gọi tường minh `Result<int>.Success(...)`.

`Error` mang **`ErrorType`** (Validation/NotFound/Conflict) chứ không phải `string` đơn thuần — nhờ đó `ResultExtensions.ToActionResult` (API, mục 5) map sang HTTP status (200/404/409/400) ở **một chỗ duy nhất**.

→ Code thật: [`../src/YTTrending.Application/Common/Models/Result.cs`](../src/YTTrending.Application/Common/Models/Result.cs) + [`Error.cs`](../src/YTTrending.Application/Common/Models/Error.cs). Lý do 3 quyết định (Value throw · private ctor · bỏ implicit) + phương án bị loại: [`decisions.md`](decisions.md) mục *Application — mục 3, Batch 1*.

## Pipeline Behaviors — chỉ 2 cái

`LoggingBehavior` (log tên request + thời gian) và `ValidationBehavior` (FluentValidation, fail → `Result.Failure`). Đăng ký trong `AddApplication()`, **`AddOpenBehavior(Logging)` trước `Validation`** (Logging bọc ngoài mới log được nhánh validation fail). → Code: [`../src/YTTrending.Application/Common/Behaviors/`](../src/YTTrending.Application/Common/Behaviors/) + [`DependencyInjection.cs`](../src/YTTrending.Application/DependencyInjection.cs); chi tiết [`decisions.md`](decisions.md) mục *Batch 5/6*.

**Không có `TransactionBehavior`.** Một `SaveChangesAsync()` đã là một transaction. Chỉ thêm behavior này khi xuất hiện handler gọi `SaveChanges` từ 2 lần trở lên — Phase 1 không có ca nào.

## EF Core mapping

Dùng Fluent API qua `IEntityTypeConfiguration<T>`, **không** rải Data Annotation lên entity — giữ Domain sạch khỏi chi tiết persistence. Code: [`../src/YTTrending.Infrastructure/Persistence/Configurations/`](../src/YTTrending.Infrastructure/Persistence/Configurations/).

Bốn điểm đáng chú ý:
- `HasQueryFilter` cho soft-delete → không phải nhớ `.Where(v => v.DeletedAt == null)` ở mọi query (khi cần xem cả bản đã xóa thì `.IgnoreQueryFilters()`).
- Cleanup Job soft-delete hàng loạt bằng `ExecuteUpdateAsync` — một câu UPDATE, không load entity lên memory.
- `HasMaxLength`/`HasPrecision` **phải khai tay** theo [`database.md`](database.md): không khai thì mọi `string` thành `text`, và `decimal` bị EF cảnh báo thiếu store type.
- **FK phải khai tay cho `VideoMetricSnapshot` và `TrendingScore`.** Hệ quả trực tiếp của "navigation 1 chiều từ `Video`": không có navigation ở cả hai chiều thì convention EF không nhận ra đây là quan hệ, `video_id` chỉ còn là cột `int` trơn không có ràng buộc.

## Background Job Hosting

**Đã chốt: `BackgroundService` + `PeriodicTimer` built-in .NET** — không thêm Hangfire/Quartz. Phase 1 single-user, chưa cần retry policy phức tạp hay UI theo dõi job.

Ba job (Sync / Metrics / Cleanup — xem [`domain/background-jobs.md`](domain/background-jobs.md)) đều theo đúng một khuôn (`PeriodicTimer` + `CreateScope()` mỗi tick + `ISender.Send(command)`). Khung code mẫu + cạm bẫy (StopHost, tick đầu, chống chạy chồng): [`../ai/setup-base-notes.md`](../ai/setup-base-notes.md) A21.

Hai điều quan trọng:

1. **Bắt buộc `CreateScope()` mỗi lần chạy.** `BackgroundService` là **singleton**, còn `ISender`/`DbContext` là **scoped** — inject thẳng qua constructor sẽ chết lúc startup, hoặc tệ hơn là giữ một `DbContext` sống mãi và phình change-tracker. Đây là lỗi kinh điển nhất khi làm background job trong ASP.NET Core.
2. **Job chỉ là cái đồng hồ.** Toàn bộ logic nằm trong Command ở Application (`Features/Jobs/`). Nhờ vậy test được logic sync mà không cần chờ timer, và gọi tay được qua API khi cần debug.

## Configuration

Phase 1 đọc config từ **`appsettings.json` + Options pattern**, validate lúc khởi động (`AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` — sai config → chết lúc start, không chết lúc job chạy 3h sáng). Handler inject `IOptionsMonitor<T>` (không phải `IOptions<T>`) để sửa `appsettings.json` là ăn ngay, khỏi restart. → Nguồn config: [`config.md`](config.md); Options class: [`../src/YTTrending.Application/Common/Options/`](../src/YTTrending.Application/Common/Options/).

> ✅ Đã chốt (pending #3 đóng): dùng `appsettings.json`, bảng `app_config` **không tạo** ở Phase 1. [`config.md`](config.md) và [`database.md`](database.md) đã sửa cho khớp.

## Thời gian: dùng `TimeProvider`, không dùng `DateTime.UtcNow`

Gần như mọi rule của dự án đều dính thời gian: `RecentDays`, velocity (views/giờ), view growth giữa 2 snapshot, `ArchivedRetentionDays`. Nếu gọi thẳng `DateTime.UtcNow` thì **không test được** — muốn kiểm tra "video quá 30 ngày bị soft-delete" phải chờ 30 ngày thật.

Inject `TimeProvider` (built-in .NET 8) vào handler, dùng `timeProvider.GetUtcNow()`. Test dùng `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) để tua thời gian tùy ý. Đăng ký: `services.AddSingleton(TimeProvider.System);`

## Testing

Một test project duy nhất: `tests/YTTrending.Application.Tests/`.

- Handler test dùng **EF Core Sqlite in-memory** (`UseSqlite("DataSource=:memory:")`), **không** dùng EF InMemory provider — provider đó không enforce unique constraint và dịch query khác hẳn Postgres, dễ test xanh nhưng chạy thật đỏ.
- `IYouTubeClient` thì mock (NSubstitute/Moq) — đây chính là lý do interface đó tồn tại.
- Ưu tiên test: Trending Engine (tính toán thuần), Discovery filter, chuyển trạng thái video.

## External Dependency

**YouTube Data API v3** — cần API key, có quota giới hạn (xem [`decisions.md`](decisions.md) pending #2). Gọi qua typed `HttpClient` + `.AddStandardResilienceHandler()` (retry + circuit breaker + timeout, 1 dòng).

Lưu ý khi quota là mối lo: quota **không** hồi lại khi retry, nên chỉ retry lỗi tạm thời (5xx, timeout) — tuyệt đối không retry lỗi 403 quota exceeded.

## Đã cân nhắc và bỏ qua

Các quyết định lớn có **lý do đầy đủ ở [`decisions.md`](decisions.md)** — không lặp lại đây: Repository per entity · `TransactionBehavior` · Entity có behavior (static factory + invariant trong entity) · MediatR 13+ (license thương mại) · Hangfire/Quartz. Danh sách "không đưa vào base": [`../ai/setup-base-notes.md`](../ai/setup-base-notes.md) A20/A25.

Còn lại là quyết định phạm-vi-Phase-1, giữ ngắn ở đây:

| Cân nhắc | Quyết định | Lý do |
|---|---|---|
| Strongly-typed ID (`VideoId` value object) | Bỏ (Phase 1) | Kéo theo `HasConversion` mọi chỗ; đã có unique index bảo vệ |
| Domain Events | Bỏ (Phase 1) | Chưa có side-effect nào cần tách khỏi luồng chính |
| .NET Aspire | Hoãn | Cân nhắc lại khi cần telemetry/dashboard sau Phase 1 |
| Tách 2 test project | Bỏ | Gộp còn 1, tách khi thật sự có thứ để test ở Infrastructure |

## Liên quan

- Database schema: [`database.md`](database.md)
- Config: [`config.md`](config.md)
- Quyết định & pending: [`decisions.md`](decisions.md)
- Toàn bộ domain/business rule: [`../AGENTS.md`](../AGENTS.md) bảng file index
