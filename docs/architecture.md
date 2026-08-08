# Architecture

## Mục tiêu

Mô tả kiến trúc kỹ thuật: layer nào chịu trách nhiệm gì, phụ thuộc theo chiều nào, và những gì **cố tình không làm** cho gọn. Business rule (discovery, trending, lifecycle...) xem [`domain/`](domain/).

Nguyên tắc xuyên suốt: **Clean Architecture + CQRS ở mức tối thiểu đủ dùng**. Mỗi lớp trừu tượng phải trả lời được "nó chặn được lỗi gì?" — không trả lời được thì bỏ.

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
YTTrending.Application     → Command/Query handler, interface (IAppDbContext, IYouTubeClient), Options
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

> **Vì sao API vẫn reference Infrastructure?** Vì API là composition root — không thể gọi `services.AddInfrastructure()` mà không "thấy" project đó. Ranh giới thật nằm ở chỗ: **Controller chỉ được biết `ISender` và DTO**, tuyệt đối không chạm `AppDbContext`. Đây là quy ước, compiler không chặn được.

> **Application vẫn phụ thuộc EF Core — và điều đó ổn.** `IAppDbContext` expose `DbSet<T>`, còn `FirstOrDefaultAsync`/`AsNoTracking` là extension của EF. Nói "Application không biết EF" là tự lừa mình. Ranh giới thật là **provider**: đổi Postgres → SQL Server chỉ động vào Infrastructure.

## Cấu trúc thư mục

```
src/
├── YTTrending.Domain/
│   ├── Entities/           Channel, Video, VideoMetricSnapshot, TrendingScore, SavedIdea
│   └── Enums/              VideoStatus (New/Tracking/Archived)
│
├── YTTrending.Application/
│   ├── Common/
│   │   ├── Interfaces/     IAppDbContext, IYouTubeClient
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
│   │   ├── AppDbContext.cs
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
  - Đây là **quy ước, không phải ràng buộc compiler**: `video.Status = ...` vẫn compile được. Đánh đổi đã biết khi chọn anemic — xem bảng "Đã cân nhắc và bỏ qua".

### YTTrending.Application
- Command/Query handler theo từng domain doc:
  - Discovery Engine — [`domain/discovery-engine.md`](domain/discovery-engine.md)
  - Trending Engine — [`domain/trending-engine.md`](domain/trending-engine.md)
  - Job orchestration — [`domain/background-jobs.md`](domain/background-jobs.md)
- Khai báo interface ra ngoài: **chỉ 2 cái** — `IAppDbContext`, `IYouTubeClient`.
- Bind + validate Options — xem [`config.md`](config.md).

### YTTrending.Infrastructure
- `AppDbContext : DbContext, IAppDbContext` + Fluent API configurations + migrations.
- `YouTubeClient` (typed `HttpClient` + resilience handler).
- 3 `BackgroundService` cho Sync / Metrics / Cleanup.
- `DependencyInjection.cs`: một extension `AddInfrastructure(config)` gom toàn bộ đăng ký.

### YTTrending.API
- `Program.cs`: `AddApplication()` + `AddInfrastructure()` + Swagger + `IExceptionHandler`.
- Controller mỏng: nhận request → `ISender.Send()` → `result.ToActionResult()`. Không if/else nghiệp vụ.

## CQRS với MediatR

### Command (luồng ghi) — qua Domain

```csharp
public record AddChannelCommand(string YoutubeChannelId) : IRequest<Result<int>>;

public class AddChannelCommandHandler(IAppDbContext db, IYouTubeClient youtube)
    : IRequestHandler<AddChannelCommand, Result<int>>
{
    public async Task<Result<int>> Handle(AddChannelCommand request, CancellationToken ct)
    {
        var exists = await db.Channels
            .AnyAsync(c => c.YoutubeChannelId == request.YoutubeChannelId, ct);
        if (exists)
            return Result<int>.Failure(Error.Conflict("channel.duplicate", "Channel đã được theo dõi"));

        var info = await youtube.GetChannelAsync(request.YoutubeChannelId, ct);
        if (info is null)
            return Result<int>.Failure(Error.NotFound("channel.not_found", "Không tìm thấy channel"));

        var channel = new Channel { YoutubeChannelId = info.Id, Name = info.Name, Url = info.Url };
        db.Channels.Add(channel);
        await db.SaveChangesAsync(ct);

        return Result<int>.Success(channel.Id);
    }
}
```

### Query (luồng đọc) — thẳng DbContext, không Repository

```csharp
public record GetDashboardQuery(int? ChannelId, decimal? MinScore) : IRequest<Result<List<VideoListItemDto>>>;

public class GetDashboardQueryHandler(IAppDbContext db)
    : IRequestHandler<GetDashboardQuery, Result<List<VideoListItemDto>>>
{
    public async Task<Result<List<VideoListItemDto>>> Handle(GetDashboardQuery q, CancellationToken ct)
    {
        var items = await db.Videos
            .AsNoTracking()
            .Where(v => q.ChannelId == null || v.ChannelId == q.ChannelId)
            .Select(v => new VideoListItemDto(v.YoutubeVideoId, v.Title, v.TrendingScore!.Score))
            .ToListAsync(ct);

        return Result<List<VideoListItemDto>>.Success(items);
    }
}
```

### Vì sao KHÔNG có Repository

Bản trước của doc này khai `IChannelRepository`, `IVideoRepository`, `IMetricsSnapshotRepository`, `ISavedIdeaRepository` — **đã bỏ hết**. Lý do:

- Repository tồn tại để đổi được nguồn dữ liệu. Ở đây chỉ có **một** DB duy nhất, vĩnh viễn.
- `DbSet<T>` + `IQueryable` đã là Repository + Unit of Work rồi. Bọc thêm một lớp chỉ để `return _db.Videos.Where(...)` là gián tiếp thừa.
- Query cần shape linh hoạt (dashboard nhiều filter, chi tiết video kèm chart) — bọc Repository sẽ đẻ ra `GetByChannelAndScoreAndDateRange...` vô tận.

`IYouTubeClient` thì **giữ**, vì nó thật sự chặn được thứ có ích: test handler không cần gọi mạng thật.

## Result Pattern

Lỗi nghiệp vụ dự kiến được (duplicate channel, không tìm thấy video) → trả `Result`. Exception chỉ dành cho lỗi hạ tầng bất thường (mất kết nối DB, bug).

```csharp
public enum ErrorType { Validation, NotFound, Conflict }

public record Error(string Code, ErrorType Type, string Message)
{
    public static Error NotFound(string code, string msg) => new(code, ErrorType.NotFound, msg);
    public static Error Conflict(string code, string msg) => new(code, ErrorType.Conflict, msg);
    public static Error Validation(string code, string msg) => new(code, ErrorType.Validation, msg);
}

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default, error);
}
```

`Error` mang **`ErrorType`** chứ không phải `string` đơn thuần — nhờ đó API map được sang HTTP status ở **một chỗ duy nhất**:

```csharp
// API/Common/ResultExtensions.cs
public static IActionResult ToActionResult<T>(this Result<T> result) => result switch
{
    { IsSuccess: true }                => new OkObjectResult(result.Value),
    { Error.Type: ErrorType.NotFound } => new NotFoundObjectResult(result.Error),
    { Error.Type: ErrorType.Conflict } => new ConflictObjectResult(result.Error),
    _                                  => new BadRequestObjectResult(result.Error)
};
```

## Pipeline Behaviors — chỉ 2 cái

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(AddChannelCommand).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));      // log tên request + thời gian chạy
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));   // FluentValidation, fail → Result.Failure
});
```

**Không có `TransactionBehavior`.** Một `SaveChangesAsync()` đã là một transaction. Chỉ thêm behavior này khi xuất hiện handler gọi `SaveChanges` từ 2 lần trở lên — Phase 1 không có ca nào.

## EF Core mapping

Dùng Fluent API qua `IEntityTypeConfiguration<T>`, **không** rải Data Annotation lên entity — giữ Domain sạch khỏi chi tiết persistence.

```csharp
public class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.HasKey(v => v.Id);
        builder.HasIndex(v => v.YoutubeVideoId).IsUnique();    // duplicate-check theo VideoId
        builder.Property(v => v.Status).HasConversion<string>();
        builder.HasQueryFilter(v => v.DeletedAt == null);      // soft-delete tự động ẩn
    }
}
```

Hai điểm đáng chú ý:
- `HasQueryFilter` cho soft-delete → không phải nhớ `.Where(v => v.DeletedAt == null)` ở mọi query (khi cần xem cả bản đã xóa thì `.IgnoreQueryFilters()`).
- Cleanup Job soft-delete hàng loạt bằng `ExecuteUpdateAsync` — một câu UPDATE, không load entity lên memory.

## Background Job Hosting

**Đã chốt: `BackgroundService` + `PeriodicTimer` built-in .NET** — không thêm Hangfire/Quartz. Phase 1 single-user, chưa cần retry policy phức tạp hay UI theo dõi job.

Ba job (Sync / Metrics / Cleanup — xem [`domain/background-jobs.md`](domain/background-jobs.md)) đều theo đúng một khuôn:

```csharp
public class SyncChannelJob(IServiceScopeFactory scopeFactory, IOptionsMonitor<TrackingOptions> options)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(options.CurrentValue.SyncIntervalHours));
        while (await timer.WaitForNextTickAsync(ct))
        {
            using var scope = scopeFactory.CreateScope();          // BẮT BUỘC
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new SyncChannelsCommand(), ct);
        }
    }
}
```

Hai điều quan trọng:

1. **Bắt buộc `CreateScope()` mỗi lần chạy.** `BackgroundService` là **singleton**, còn `ISender`/`DbContext` là **scoped** — inject thẳng qua constructor sẽ chết lúc startup, hoặc tệ hơn là giữ một `DbContext` sống mãi và phình change-tracker. Đây là lỗi kinh điển nhất khi làm background job trong ASP.NET Core.
2. **Job chỉ là cái đồng hồ.** Toàn bộ logic nằm trong Command ở Application (`Features/Jobs/`). Nhờ vậy test được logic sync mà không cần chờ timer, và gọi tay được qua API khi cần debug.

## Configuration

Phase 1 đọc config từ **`appsettings.json` + Options pattern**, validate lúc khởi động:

```csharp
services.AddOptions<TrackingOptions>()
    .Bind(config.GetSection("Tracking"))
    .ValidateDataAnnotations()
    .ValidateOnStart();     // sai config → chết lúc start, không chết lúc job chạy 3h sáng
```

Handler inject `IOptionsMonitor<T>` (không phải `IOptions<T>`) để sửa `appsettings.json` là ăn ngay, khỏi restart.

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

**YouTube Data API v3** — cần API key, có quota giới hạn (xem [`decisions.md`](decisions.md) pending #2). Gọi qua typed `HttpClient`:

```csharp
services.AddHttpClient<IYouTubeClient, YouTubeClient>()
        .AddStandardResilienceHandler();   // retry + circuit breaker + timeout, 1 dòng
```

Lưu ý khi quota là mối lo: quota **không** hồi lại khi retry, nên chỉ retry lỗi tạm thời (5xx, timeout) — tuyệt đối không retry lỗi 403 quota exceeded.

## Đã cân nhắc và bỏ qua

| Cân nhắc | Quyết định | Lý do |
|---|---|---|
| Repository cho từng entity | **Bỏ** | Chỉ có 1 DB, `DbSet` đã là repository — xem mục trên |
| `TransactionBehavior` | **Bỏ** | 1 `SaveChangesAsync` = 1 transaction, Phase 1 không có handler nào ghi 2 lần |
| Entity có behavior (static factory + private setter + invariant trong entity) | **Bỏ** (chốt 07/08/2026, sau khi đã làm xong rồi đổi) | Đo thực tế: cả Domain chỉ có **2 câu `if`**, còn 8/15 method là nghi lễ gán thuần quanh `private set`. Đổi sang property bag + `required`; 2 invariant dời sang `VideoStateRules` ở Application. Đánh đổi: rule terminal-state từ ràng buộc compiler thành quy ước, và test chuyển trạng thái từ unit test thành test qua handler |
| Strongly-typed ID (`VideoId` value object) | **Bỏ (Phase 1)** | Hay để học nhưng kéo theo `HasConversion` ở mọi chỗ; đã có unique index bảo vệ |
| Domain Events | **Bỏ (Phase 1)** | Chưa có side-effect nào cần tách khỏi luồng chính |
| MediatR 13+ | **Không nâng** | License thương mại — dừng ở dòng 12.x (Apache-2.0) |
| Hangfire / Quartz.NET | **Bỏ** | `BackgroundService` đủ cho single-user, không cần thêm dependency + bảng job |
| .NET Aspire | **Hoãn** | Cân nhắc lại sau khi Phase 1 chạy được, khi cần telemetry/dashboard |
| Tách 2 test project | **Bỏ** | Gộp còn 1, tách khi thật sự có thứ để test ở Infrastructure |

## Liên quan

- Database schema: [`database.md`](database.md)
- Config: [`config.md`](config.md)
- Quyết định & pending: [`decisions.md`](decisions.md)
- Toàn bộ domain/business rule: [`../AGENTS.md`](../AGENTS.md) bảng file index
