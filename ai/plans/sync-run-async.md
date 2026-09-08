# SyncRun async — các lát nhỏ có thể nghiệm thu ngay

> ⚠️ **FILE TẠM — không commit; chỉ xoá khi toàn bộ batch bên dưới đã hoàn tất.**
>
> Plan này thay thế hướng Sync-all synchronous cũ. Metrics Update không thuộc scope.
> Quyết định kiến trúc là nguồn “vì sao”: [`../../docs/decisions.md`](../../docs/decisions.md), mục
> *Sync-all async qua SyncRun*. Code là nguồn sự thật; plan chỉ chia việc và nêu điều kiện nghiệm thu.

## Mục tiêu cuối

`POST /api/jobs/sync` trả ngay một `SyncRun` để FE poll tiến độ. Worker xử lý tuần tự từng
`SyncRunItem` bằng `SyncChannelCommand`; scheduler cũng chỉ tạo `SyncRun`, không có loop sync-all
riêng.

```text
Manual POST / Scheduler tick
            │
            ▼
 CreateSyncRunCommand ──► SyncRun + SyncRunItem trong DB ──► queue(runId)
                                                                    │
                                                                    ▼
                                                            SyncRunWorker
                                                                    │
                                                                    ▼
                                          SyncChannelCommand(channelId, triggerType)
```

### Từ điển đọc plan

- **Run**: một lần Sync all. Nó giữ status và counter tổng.
- **Item**: snapshot công việc của đúng một channel trong một run. `ChannelId` là historical
  reference, không có FK tới `Channel`; `ChannelName` là tên tại lúc tạo run.
- **Pending / Running**: chưa terminal. `Succeeded`, `Skipped`, `Failed` là terminal cho item;
  `Completed`, `CompletedWithIssues`, `Interrupted`, `Failed` là terminal cho run.
- **Durable progress**: sau mỗi chuyển trạng thái quan trọng worker phải save ngay. Khi app crash,
  DB vẫn phản ánh đúng mốc đã đạt được.
- **Queue chỉ có `runId`**: queue in-memory chỉ đánh thức worker. Item, status và counter luôn đọc
  từ DB, nên DB mới là source of truth.

## Quy tắc áp dụng cho mọi batch

1. Trước khi sửa source, đọc `docs/coding-convention.md`; khi sửa domain behaviour, đọc tài liệu
   domain tương ứng và decision SyncRun.
2. Không viết/chỉnh tay migration. Chỉ EF CLI tạo migration, sau đó review diff migration sinh ra.
3. Không thêm test project/CI trong Phase 1. Mỗi batch phải có `dotnet build`; batch có API/migration
   phải nghiệm thu ngay bằng Swagger/API hoặc `dotnet ef`.
4. Không làm trước việc của batch sau “cho tiện”. Nếu cần type chung, chỉ tạo đúng phần cần cho hành vi
   đang nghiệm thu.
5. Sau batch có thay đổi contract/schema/job, cập nhật tài liệu SSOT liên quan ngay trong batch đó;
   cập nhật `ai/current.md` khi trạng thái công việc đổi. Lịch sử chỉ ghi khi batch thực sự xong.
6. Không đổi `SyncChannelCommand` thành sync-all. Nó vẫn là đơn vị atomic sync **một** channel.

## Preflight cấu trúc — làm một lần sau Batch 1, trước Batch 2

- Đọc lại `docs/coding-convention.md` và decision *Sync-all async qua SyncRun*.
- Chạy `git status --short`. Chỉ được có một định nghĩa mỗi type: `SyncRun`, `SyncRunItem`,
  `SyncRunTriggerType`, `SyncRunStatus`, `SyncRunItemStatus`.
- Canonical enum path là `src/YTTrending.Domain/Enums/`, namespace `YTTrending.Domain.Enums`.
  Nếu có draft dưới `Enums/SyncRuns/`, đối chiếu rồi di chuyển hoặc xoá bản trùng trước build.
- Tên counter thống nhất là `SuccessCount`, không dùng `SucceededCount`.
- Xác nhận baseline vẫn còn: `TrackingOptions.SyncIntervalHours`,
  `TrackingOptions.ManualSyncCooldownHours`, `JobOptions.SyncEnabled`, JSON enum converter,
  và package Hosting Abstractions.
- **Hoàn tất refactor folder thành một commit `chore` riêng, không trộn hành vi Batch 2:** di chuyển
  `Application/Common/Models` vào `Results`/`Pagination`/`Filter`/`Youtube`/`Sync`; interface vào
  `Concurrency`/`Integrations`/`Jobs`/`Persistence`/`Services`; `ChannelSyncLock` vào
  `Infrastructure/Concurrency`; sau đó tạo `Infrastructure/Jobs/Sync` cho queue/worker/scheduler.
  Đổi namespace theo folder, bổ sung leaf namespace mới vào `GlobalUsings.cs` của các project tiêu thụ,
  sửa các `using` cũ, rồi `dotnet build` trước khi vào Batch 2. `IChannelSyncLock` và
  `ISyncRunCreationLock` luôn là `Concurrency`; `ISyncRunQueue` luôn là `Jobs`.

---

## Batch 0 — Chốt decision và plan ✅

Đã hoàn thành: decision SyncRun async đã có trong `docs/decisions.md` và file plan này là hướng
triển khai tạm thời. Không có code hoặc migration thuộc batch này.

---

## Batch 1 — Schema SyncRun có thể migrate độc lập

**Một việc duy nhất:** đưa hai bảng và quan hệ của chúng vào database. Chưa có repository, API,
queue hoặc background service.

### Làm

1. Tạo các enum domain: `SyncRunTriggerType`, `SyncRunStatus`, `SyncRunItemStatus`.
2. Tạo entity `SyncRun` và `SyncRunItem` với đúng contract:

   ```text
   SyncRun: Id, TriggerType, Status,
            TotalCount, SuccessCount, SkippedCount, FailedCount,
            ErrorCode?, ErrorMessage?, CreatedAt, StartedAt?, CompletedAt?

   SyncRunItem: Id, SyncRunId, ChannelId, ChannelName,
                Status, ErrorCode?, ErrorMessage?, StartedAt?, CompletedAt?
   ```

3. Tạo `SyncRunConfiguration` và `SyncRunItemConfiguration`; thêm hai `DbSet` vào
   `YTTrendingDbContext`.
4. Mapping bắt buộc:

   - enum lưu `VARCHAR`; max length trigger 16, run status 32, item status 16;
   - `ErrorCode` 128, `ErrorMessage` 1024, `ChannelName` 255;
   - FK cascade **chỉ** `SyncRunItem.SyncRunId → SyncRun.Id`;
   - không có FK `ChannelId → Channel.Id`;
   - unique index `(SyncRunId, ChannelId)`, thêm index `(SyncRunId, Status)` và index `SyncRun.Status`.

5. Chạy EF tạo migration (tên gợi ý `AddSyncRuns`), review migration, rồi apply database.
6. Cập nhật phần schema thực tế vào `docs/database.md` ngay sau khi migration được chấp nhận.

### Được phép sửa

```text
src/YTTrending.Domain/Enums/{SyncRunTriggerType,SyncRunStatus,SyncRunItemStatus}.cs
src/YTTrending.Domain/Entities/{SyncRun,SyncRunItem}.cs
src/YTTrending.Infrastructure/Persistence/Configurations/{SyncRunConfiguration,SyncRunItemConfiguration}.cs
src/YTTrending.Infrastructure/Persistence/YTTrendingDbContext.cs
src/YTTrending.Infrastructure/Persistence/Migrations/    # chỉ EF CLI sinh
docs/database.md
ai/current.md
```

### Code copy-paste — Batch 1

`src/YTTrending.Domain/Enums/SyncRunTriggerType.cs`

```csharp
namespace YTTrending.Domain.Enums;

public enum SyncRunTriggerType
{
    // Người dùng chủ động bấm Sync all.
    Manual,
    // Background job định kỳ tạo run.
    Scheduled,
}
```

`src/YTTrending.Domain/Enums/SyncRunStatus.cs`

```csharp
namespace YTTrending.Domain.Enums;

public enum SyncRunStatus
{
    // Đã lưu và enqueue, worker chưa bắt đầu.
    Pending,
    // Worker đang xử lý các item của run.
    Running,
    // Mọi item đều thành công.
    Completed,
    // Run đã xong nhưng có item Skipped hoặc Failed.
    CompletedWithIssues,
    // App dừng/crash khi run chưa xong; không resume.
    Interrupted,
    // Processor gặp lỗi cấp run, không thể hoàn tất.
    Failed,
}
```

`src/YTTrending.Domain/Enums/SyncRunItemStatus.cs`

```csharp
namespace YTTrending.Domain.Enums;

public enum SyncRunItemStatus
{
    Pending,
    Running,
    Succeeded,
    Skipped,
    Failed,
}
```

`src/YTTrending.Domain/Entities/SyncRun.cs`

```csharp
namespace YTTrending.Domain.Entities;

/// <summary>Bản ghi tiến độ bền vững của một lần Sync all.</summary>
public class SyncRun
{
    public int Id { get; set; }

    // Trigger quyết định cooldown của từng channel ở Batch 5.
    public required SyncRunTriggerType TriggerType { get; set; }
    public SyncRunStatus Status { get; set; } = SyncRunStatus.Pending;

    // Snapshot số channel enabled khi tạo run; không đổi nếu user bật/tắt channel sau đó.
    public required int TotalCount { get; set; }
    // ProcessedCount được suy ra bằng tổng ba counter terminal, không lưu cột riêng.
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }

    // Chỉ chứa lỗi làm hỏng cả run; lỗi từng channel nằm ở SyncRunItem.
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    // Thời điểm nghiệp vụ do processor set, không phải audit field do EF tự điền.
    public required DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
```

`src/YTTrending.Domain/Entities/SyncRunItem.cs`

```csharp
namespace YTTrending.Domain.Entities;

/// <summary>Snapshot công việc và kết quả của một channel trong <see cref="SyncRun"/>.</summary>
public class SyncRunItem
{
    public int Id { get; set; }

    // EF lấy FK từ navigation SyncRun khi save run và item cùng lúc.
    // Không required vì object initializer chưa biết Id của run trước SaveChangesAsync.
    public int SyncRunId { get; set; }

    // Historical reference: không FK tới Channel để xóa channel vẫn giữ lịch sử run.
    public required int ChannelId { get; set; }
    public required string ChannelName { get; set; }

    public SyncRunItemStatus Status { get; set; } = SyncRunItemStatus.Pending;

    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public required SyncRun SyncRun { get; set; }
}
```

`src/YTTrending.Infrastructure/Persistence/Configurations/SyncRunConfiguration.cs`

```csharp
namespace YTTrending.Infrastructure.Persistence.Configurations;

public sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.Property(x => x.TriggerType).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.TotalCount).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(128);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1024);
        builder.HasIndex(x => x.Status);
    }
}
```

`src/YTTrending.Infrastructure/Persistence/Configurations/SyncRunItemConfiguration.cs`

```csharp
namespace YTTrending.Infrastructure.Persistence.Configurations;

public sealed class SyncRunItemConfiguration : IEntityTypeConfiguration<SyncRunItem>
{
    public void Configure(EntityTypeBuilder<SyncRunItem> builder)
    {
        builder.Property(x => x.ChannelId).IsRequired();
        builder.Property(x => x.ChannelName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.ErrorCode).HasMaxLength(128);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1024);

        // FK duy nhất của item; xóa run thì xóa history item của chính run đó.
        builder.HasOne(x => x.SyncRun)
            .WithMany()
            .HasForeignKey(x => x.SyncRunId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SyncRunId, x.ChannelId }).IsUnique();
        builder.HasIndex(x => new { x.SyncRunId, x.Status });
    }
}
```

**So với file cũ, chỉ thêm:** hai `DbSet` là `SyncRuns` và `SyncRunItems`. Giữ nguyên
`OnModelCreating`, audit pipeline và mọi `DbSet` đã có.

Thay toàn bộ `src/YTTrending.Infrastructure/Persistence/YTTrendingDbContext.cs`:

```csharp
namespace YTTrending.Infrastructure.Persistence;

public class YTTrendingDbContext(DbContextOptions<YTTrendingDbContext> options, TimeProvider clock)
    : DbContext(options)
{
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Video> Videos => Set<Video>();
    public DbSet<VideoMetricSnapshot> VideoMetricSnapshots => Set<VideoMetricSnapshot>();
    public DbSet<TrendingScore> TrendingScores => Set<TrendingScore>();
    public DbSet<SavedIdea> SavedIdeas => Set<SavedIdea>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<SyncRunItem> SyncRunItems => Set<SyncRunItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(YTTrendingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(ct);
    }

    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    private void ApplyAuditFields()
    {
        // Chỉ entity kế thừa AuditableEntity được EF tự điền audit time.
        var now = clock.GetUtcNow();

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Property(nameof(AuditableEntity.CreatedAt)).CurrentValue = now;

            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Property(nameof(AuditableEntity.UpdatedAt)).CurrentValue = now;
        }
    }
}
```

### Nghiệm thu — dừng Batch 1 tại đây

```powershell
dotnet build
dotnet ef migrations add AddSyncRuns -p src/YTTrending.Infrastructure -s src/YTTrending.API
dotnet ef database update -p src/YTTrending.Infrastructure -s src/YTTrending.API
```

- Build pass và EF tạo/apply migration thành công.
- Review migration thấy đúng hai bảng, cột nullable, VARCHAR, index và duy nhất một FK item → run.
- Chưa xuất hiện endpoint `/api/jobs`, repository hay hosted service.

---

## Batch 2 — Tạo một run Pending qua manual API

**Một lát chức năng:** người dùng bấm Sync all, backend snapshot channel enabled thành run + item,
trả `202`; run chưa được xử lý. Đây là batch nhỏ nhất có thể kiểm tra được hành vi tạo run thật.

### Làm

1. Thêm phần repository tối thiểu để tạo/đọc summary và chặn active run:
   `HasActiveAsync`, `GetSummaryByIdAsync`, `CreateItems`; thêm `GetEnabledAsync()` vào
   `IChannelRepository`/`ChannelRepository`. Entity phục vụ tạo phải tracked, summary API no-tracking.
2. Thêm `ISyncRunQueue`/`SyncRunQueue` (unbounded `Channel<int>`, một reader) và
   `ISyncRunCreationLock`/implementation singleton. Queue chỉ nhận `runId`, không chứa channel/item.
3. Thêm `SyncRunDto`, mapping DTO, `SyncRunErrors`, `CreateSyncRunCommand` + handler.
4. Handler theo thứ tự: lấy creation lock → kiểm tra `HasActiveAsync` → lấy enabled channels → lấy một
   `now` → tạo `SyncRun(Pending)` và item snapshot → save **một lần** → `Enqueue(run.Id)` → release lock.
   `items` gắn navigation `SyncRun` để EF điền FK sau khi run được insert.
5. Thêm `POST /api/jobs/sync` và `GET /api/jobs/sync/{id}`. POST trả `202 Accepted`; GET chỉ trả
   summary. Chưa có endpoint xem item và chưa đăng ký worker.
6. Đăng ký repository scoped; queue và creation lock singleton. Chưa cập nhật
   `docs/api-contract.md`: theo decision, chỉ ghi contract SyncRun khi Batch 3 đã có đủ endpoint
   summary và item paged.

### Được phép sửa/tạo

```text
Application/Common/Interfaces/Persistence/{IChannelRepository,ISyncRunRepository}.cs
Application/Common/Interfaces/Jobs/ISyncRunQueue.cs
Application/Common/Interfaces/Concurrency/ISyncRunCreationLock.cs
Application/Features/Jobs/{Dtos/SyncRunDto.cs,SyncRunErrors.cs,
  Commands/CreateSyncRun/*,Queries/GetSyncRun/*}
Infrastructure/Persistence/Repositories/{SyncRunRepository,ChannelRepository}.cs
Infrastructure/Jobs/Sync/SyncRunQueue.cs
Infrastructure/Concurrency/SyncRunCreationLock.cs
Infrastructure/DependencyInjection.cs
API/Controllers/JobsController.cs
ai/current.md
```

### Code copy-paste — Batch 2

`src/YTTrending.Application/Common/Interfaces/Persistence/ISyncRunRepository.cs`

```csharp
namespace YTTrending.Application.Common.Interfaces.Persistence;

public interface ISyncRunRepository : IRepository<SyncRun>
{
    // Pending/Running đều active, nên chỉ có một batch Sync all ở Phase 1.
    Task<bool> HasActiveAsync(CancellationToken ct);
    // Bản đọc no-tracking cho API summary, không dùng để cập nhật progress.
    Task<SyncRun?> GetSummaryByIdAsync(int id, CancellationToken ct);
    void CreateItems(IEnumerable<SyncRunItem> items);
}
```

`src/YTTrending.Application/Common/Interfaces/Jobs/ISyncRunQueue.cs`

```csharp
namespace YTTrending.Application.Common.Interfaces.Jobs;

public interface ISyncRunQueue
{
    // Chỉ truyền run id qua memory; item/progress thật luôn nằm trong DB.
    void Enqueue(int syncRunId);
    ValueTask<int> DequeueAsync(CancellationToken ct);
}
```

`src/YTTrending.Application/Common/Interfaces/Concurrency/ISyncRunCreationLock.cs`

```csharp
namespace YTTrending.Application.Common.Interfaces.Concurrency;

public interface ISyncRunCreationLock
{
    // Khóa đoạn check-active + create để controller/scheduler không tạo hai run cùng lúc.
    bool TryAcquire();
    void Release();
}
```

`src/YTTrending.Infrastructure/Persistence/Repositories/SyncRunRepository.cs`

```csharp
using YTTrending.Application.Common.Interfaces;
using YTTrending.Domain.Enums;

namespace YTTrending.Infrastructure.Persistence.Repositories;

public sealed class SyncRunRepository(YTTrendingDbContext db)
    : Repository<SyncRun>(db), ISyncRunRepository
{
    public Task<bool> HasActiveAsync(CancellationToken ct) =>
        Set.AnyAsync(x => x.Status == SyncRunStatus.Pending || x.Status == SyncRunStatus.Running, ct);

    public Task<SyncRun?> GetSummaryByIdAsync(int id, CancellationToken ct) =>
        Set.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public void CreateItems(IEnumerable<SyncRunItem> items) => db.SyncRunItems.AddRange(items);
}
```

**So với file cũ, chỉ thêm:** `GetEnabledAsync(CancellationToken)` để lấy channel đang bật theo
`Id` tăng dần. Không đổi contract CRUD/paging hiện có.

Thay toàn bộ `src/YTTrending.Application/Common/Interfaces/Persistence/IChannelRepository.cs`:

```csharp
namespace YTTrending.Application.Common.Interfaces.Persistence;

public interface IChannelRepository : IRepository<Channel>
{
    Task<bool> ExistsByYoutubeIdAsync(string youtubeChannelId, CancellationToken ct);
    // Tracked vì CreateSyncRunHandler cần snapshot entity tại thời điểm tạo run.
    Task<List<Channel>> GetEnabledAsync(CancellationToken ct);
    Task<PagedResult<Channel>> GetPagedAsync(ChannelFilter filter, CancellationToken ct);
}
```

**So với file cũ, chỉ thêm:** implementation `GetEnabledAsync`, lọc `IsEnabled` và không dùng
`AsNoTracking` vì handler cần entity tracked. Các method cũ giữ nguyên.

Thay toàn bộ `src/YTTrending.Infrastructure/Persistence/Repositories/ChannelRepository.cs`:

```csharp
using YTTrending.Application.Common.Extensions;
using YTTrending.Application.Common.Models;

namespace YTTrending.Infrastructure.Persistence.Repositories;

public sealed class ChannelRepository(YTTrendingDbContext db)
    : Repository<Channel>(db), IChannelRepository
{
    public Task<bool> ExistsByYoutubeIdAsync(string youtubeChannelId, CancellationToken ct) =>
        Set.AnyAsync(c => c.YoutubeChannelId == youtubeChannelId, ct);

    public Task<List<Channel>> GetEnabledAsync(CancellationToken ct) =>
        Set.Where(x => x.IsEnabled).OrderBy(x => x.Id).ToListAsync(ct);

    public Task<PagedResult<Channel>> GetPagedAsync(ChannelFilter filter, CancellationToken ct) =>
        Set.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);
}
```

`src/YTTrending.Infrastructure/Jobs/Sync/SyncRunQueue.cs`

```csharp
using System.Threading.Channels;
using YTTrending.Application.Common.Interfaces;

namespace YTTrending.Infrastructure.Jobs.Sync;

public sealed class SyncRunQueue : ISyncRunQueue
{
    // Queue in-memory chỉ đánh thức một worker; DB mới là source of truth.
    private readonly Channel<int> _queue = Channel.CreateUnbounded<int>(new()
    {
        SingleReader = true,
    });

    public void Enqueue(int syncRunId)
    {
        if (!_queue.Writer.TryWrite(syncRunId))
            throw new InvalidOperationException("Unable to enqueue sync run.");
    }

    public ValueTask<int> DequeueAsync(CancellationToken ct) => _queue.Reader.ReadAsync(ct);
}
```

`src/YTTrending.Infrastructure/Concurrency/SyncRunCreationLock.cs`

```csharp
using YTTrending.Application.Common.Interfaces;

namespace YTTrending.Infrastructure.Concurrency;

public sealed class SyncRunCreationLock : ISyncRunCreationLock
{
    // 0 = rảnh, 1 = đang tạo run; Interlocked giúp singleton an toàn giữa request/job.
    private int _isHeld;

    public bool TryAcquire() => Interlocked.CompareExchange(ref _isHeld, 1, 0) == 0;
    public void Release() => Volatile.Write(ref _isHeld, 0);
}
```

`src/YTTrending.Application/Features/Jobs/Dtos/SyncRunDto.cs`

```csharp
namespace YTTrending.Application.Features.Jobs.Dtos;

public sealed record SyncRunDto(
    int Id,
    SyncRunTriggerType TriggerType,
    SyncRunStatus Status,
    int TotalCount,
    int SuccessCount,
    int SkippedCount,
    int FailedCount,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt)
{
    // Không lưu DB vì luôn suy ra được từ các counter item đã terminal.
    public int ProcessedCount => SuccessCount + SkippedCount + FailedCount;
}

public static class SyncRunMappings
{
    // API trả DTO, không trả EF entity trực tiếp cho FE.
    public static SyncRunDto ToDto(this SyncRun run) => new(
        run.Id, run.TriggerType, run.Status, run.TotalCount, run.SuccessCount,
        run.SkippedCount, run.FailedCount, run.ErrorCode, run.ErrorMessage,
        run.CreatedAt, run.StartedAt, run.CompletedAt);
}
```

`src/YTTrending.Application/Features/Jobs/SyncRunErrors.cs`

```csharp
namespace YTTrending.Application.Features.Jobs;

public static class SyncRunErrors
{
    public const string InProgress = "syncRun.inProgress";
    public const string NoEnabledChannels = "syncRun.noEnabledChannels";
    public const string NotFound = "syncRun.notFound";
}
```

`src/YTTrending.Application/Features/Jobs/Commands/CreateSyncRun/CreateSyncRunCommand.cs`

```csharp
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Commands.CreateSyncRun;

public sealed record CreateSyncRunCommand(SyncRunTriggerType TriggerType)
    : IRequest<Result<SyncRunDto>>;
```

`src/YTTrending.Application/Features/Jobs/Commands/CreateSyncRun/CreateSyncRunCommandHandler.cs`

```csharp
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Commands.CreateSyncRun;

public sealed class CreateSyncRunCommandHandler(
    ISyncRunRepository syncRuns,
    IChannelRepository channels,
    IUnitOfWork uow,
    ISyncRunQueue queue,
    ISyncRunCreationLock creationLock,
    TimeProvider clock)
    : IRequestHandler<CreateSyncRunCommand, Result<SyncRunDto>>
{
    public async Task<Result<SyncRunDto>> Handle(CreateSyncRunCommand command, CancellationToken ct)
    {
        // Khóa chỉ bảo vệ lúc tạo run; worker sẽ xử lý ở batch sau.
        if (!creationLock.TryAcquire())
        {
            return Result<SyncRunDto>.Failure(Error.Conflict(
                SyncRunErrors.InProgress,
                "Một lượt sync toàn bộ đang được tạo hoặc xử lý."));
        }

        try
        {
            if (await syncRuns.HasActiveAsync(ct))
            {
                return Result<SyncRunDto>.Failure(Error.Conflict(
                    SyncRunErrors.InProgress,
                    "Một lượt sync toàn bộ đang được xử lý."));
            }

            // Đây là snapshot target: bật/tắt channel sau dòng này không đổi item đã tạo.
            var enabledChannels = await channels.GetEnabledAsync(ct);
            if (enabledChannels.Count == 0)
            {
                return Result<SyncRunDto>.Failure(Error.Conflict(
                    SyncRunErrors.NoEnabledChannels,
                    "Không có channel nào đang bật sync."));
            }

            // Một mốc UTC chung cho run và tất cả item của batch.
            var now = clock.GetUtcNow();
            var run = new SyncRun
            {
                TriggerType = command.TriggerType,
                Status = SyncRunStatus.Pending,
                TotalCount = enabledChannels.Count,
                CreatedAt = now,
            };
            // Navigation giúp EF tự gán SyncRunId sau khi insert run.
            var items = enabledChannels.Select(channel => new SyncRunItem
            {
                ChannelId = channel.Id,
                ChannelName = channel.Name,
                SyncRun = run,
            }).ToList();

            syncRuns.Create(run);
            syncRuns.CreateItems(items);
            await uow.SaveChangesAsync(ct);

            // Save thành công mới enqueue để worker không đọc một run chưa tồn tại.
            queue.Enqueue(run.Id);
            return Result<SyncRunDto>.Success(run.ToDto());
        }
        finally
        {
            creationLock.Release();
        }
    }
}
```

`src/YTTrending.Application/Features/Jobs/Queries/GetSyncRun/GetSyncRunQuery.cs`

```csharp
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRun;

public sealed record GetSyncRunQuery(int Id) : IRequest<Result<SyncRunDto>>;
```

`src/YTTrending.Application/Features/Jobs/Queries/GetSyncRun/GetSyncRunQueryHandler.cs`

```csharp
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRun;

public sealed class GetSyncRunQueryHandler(ISyncRunRepository syncRuns)
    : IRequestHandler<GetSyncRunQuery, Result<SyncRunDto>>
{
    public async Task<Result<SyncRunDto>> Handle(GetSyncRunQuery query, CancellationToken ct)
    {
        var run = await syncRuns.GetSummaryByIdAsync(query.Id, ct);
        return run is null
            ? Result<SyncRunDto>.Failure(Error.NotFound(SyncRunErrors.NotFound, "Không tìm thấy lượt sync."))
            : Result<SyncRunDto>.Success(run.ToDto());
    }
}
```

`src/YTTrending.Application/Features/Jobs/Queries/GetSyncRun/GetSyncRunQueryValidator.cs`

```csharp
namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRun;

public sealed class GetSyncRunQueryValidator : AbstractValidator<GetSyncRunQuery>
{
    public GetSyncRunQueryValidator() => RuleFor(x => x.Id).GreaterThan(0);
}
```

Tạo mới `src/YTTrending.API/Controllers/JobsController.cs`:

```csharp
using YTTrending.Application.Features.Jobs.Commands.CreateSyncRun;
using YTTrending.Application.Features.Jobs.Queries.GetSyncRun;
using YTTrending.Domain.Enums;

namespace YTTrending.API.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(ISender sender) : ControllerBase
{
    [HttpPost("sync")]
    public async Task<IActionResult> CreateSyncRun(CancellationToken ct)
    {
        var result = await sender.Send(new CreateSyncRunCommand(SyncRunTriggerType.Manual), ct);
        return result.IsSuccess
            ? Accepted(result.Value)
            : result.ToActionResult();
    }

    [HttpGet("sync/{id:int}")]
    public async Task<IActionResult> GetSyncRun(int id, CancellationToken ct)
        => (await sender.Send(new GetSyncRunQuery(id), ct)).ToActionResult();
}
```

**So với file cũ, thêm:**

- `using YTTrending.Infrastructure.Concurrency` và `using YTTrending.Infrastructure.Jobs.Sync`;
- `ISyncRunRepository` scoped;
- `ISyncRunQueue` và `ISyncRunCreationLock` singleton.

Chưa đăng ký hosted service ở Batch 2.

Thay toàn bộ `src/YTTrending.Infrastructure/DependencyInjection.cs`:

```csharp
using YTTrending.Application.Common.Interfaces;
using YTTrending.Infrastructure.Concurrency;
using YTTrending.Infrastructure.Jobs.Sync;
using YTTrending.Infrastructure.Persistence;
using YTTrending.Infrastructure.Persistence.Repositories;
using YTTrending.Infrastructure.YouTube;

namespace YTTrending.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<YTTrendingDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("Default"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<ISyncRunRepository, SyncRunRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IChannelSyncLock, ChannelSyncLock>();
        services.AddSingleton<ISyncRunQueue, SyncRunQueue>();
        services.AddSingleton<ISyncRunCreationLock, SyncRunCreationLock>();

        if (configuration.GetValue("YouTube:UseFake", true))
        {
            services.AddSingleton<IYouTubeClient, FakeYouTubeClient>();
        }
        else
        {
            services.AddHttpClient<IYouTubeClient, YouTubeClient>(client =>
            {
                client.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/");
            });
        }

        return services;
    }
}
```

### Nghiệm thu — dừng Batch 2 tại đây

1. Chạy `dotnet build`, API với fake YouTube và mở Swagger.
2. Có ít nhất một enabled channel: `POST /api/jobs/sync` trả `202`, body là `SyncRunDto` có
   `status = Pending`, `totalCount` đúng số channel enabled, ba counter bằng 0.
3. `GET /api/jobs/sync/{id}` trả đúng summary Pending. `POST` lần hai trả `409 syncRun.inProgress`.
4. Kiểm tra DB: mỗi channel enabled lúc POST có đúng một item với `ChannelName` snapshot và Pending.
5. Dừng API sau khi ghi lại `id` của run Pending. Queue in-memory mất theo process là chủ ý; run này là
   dữ liệu đầu vào để nghiệm thu recovery ở Batch 4.

---

## Batch 3 — Đọc danh sách item phân trang

**Một lát chức năng:** FE có đủ read API để xem target snapshot và item lỗi của một run, dù worker
chưa tồn tại. Không thêm xử lý sync.

### Làm

1. Thêm `SyncRunItemFilter : PagedQuery`; `SyncRunId` luôn được controller gán từ route,
   `Status` là optional filter.
2. Mở rộng repository bằng `GetItemsPagedAsync`: no-tracking, `WhereIf` status, `OrderBy(Id)`,
   `ToPagedResultAsync`.
3. Thêm `SyncRunItemDto`, mapping, `GetSyncRunItemsQuery` + handler + validator.
4. Thêm `GET /api/jobs/sync/{id}/items`. Dùng `query with { SyncRunId = id }` để route id thắng input
   query string; run không tồn tại phải trả `404 syncRun.notFound`, không trả page rỗng.
5. Hoàn thiện contract item/pagination/error code trong `docs/api-contract.md`.

### Được phép sửa/tạo

```text
Application/Common/Models/Filter/SyncRunItemFilter.cs
Application/Common/Interfaces/Persistence/ISyncRunRepository.cs
Application/Features/Jobs/Dtos/SyncRunItemDto.cs
Application/Features/Jobs/Queries/GetSyncRunItems/*
Infrastructure/Persistence/Repositories/SyncRunRepository.cs
API/Controllers/JobsController.cs
docs/api-contract.md
ai/current.md
```

### Code copy-paste — Batch 3

`src/YTTrending.Application/Common/Models/Filter/SyncRunItemFilter.cs`

```csharp
namespace YTTrending.Application.Common.Models.Filter;

public record SyncRunItemFilter : PagedQuery
{
    // Controller gán từ route, không tin SyncRunId client có thể gửi trong query string.
    public int SyncRunId { get; init; }
    // Null nghĩa là không lọc theo trạng thái.
    public SyncRunItemStatus? Status { get; init; }
}
```

**So với bản của Batch 2, chỉ thêm:** `GetItemsPagedAsync(SyncRunItemFilter, ct)` cho endpoint
item list. Các method tạo run/đọc summary giữ nguyên.

Thay toàn bộ `src/YTTrending.Application/Common/Interfaces/Persistence/ISyncRunRepository.cs`:

```csharp
namespace YTTrending.Application.Common.Interfaces.Persistence;

public interface ISyncRunRepository : IRepository<SyncRun>
{
    Task<bool> HasActiveAsync(CancellationToken ct);
    Task<SyncRun?> GetSummaryByIdAsync(int id, CancellationToken ct);
    // Chỉ đọc cho API; entity no-tracking để không giữ change tracker cho cả trang dữ liệu.
    Task<PagedResult<SyncRunItem>> GetItemsPagedAsync(SyncRunItemFilter filter, CancellationToken ct);
    void CreateItems(IEnumerable<SyncRunItem> items);
}
```

**So với bản của Batch 2, chỉ thêm:** import cho paging và `GetItemsPagedAsync` dùng
`AsNoTracking` + optional status filter + `OrderBy(Id)`. Không đổi query summary hay create item.

Thay toàn bộ `src/YTTrending.Infrastructure/Persistence/Repositories/SyncRunRepository.cs`:

```csharp
using YTTrending.Application.Common.Extensions;
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Common.Models;
using YTTrending.Domain.Enums;

namespace YTTrending.Infrastructure.Persistence.Repositories;

public sealed class SyncRunRepository(YTTrendingDbContext db)
    : Repository<SyncRun>(db), ISyncRunRepository
{
    public Task<bool> HasActiveAsync(CancellationToken ct) =>
        Set.AnyAsync(x => x.Status == SyncRunStatus.Pending || x.Status == SyncRunStatus.Running, ct);

    public Task<SyncRun?> GetSummaryByIdAsync(int id, CancellationToken ct) =>
        Set.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<PagedResult<SyncRunItem>> GetItemsPagedAsync(SyncRunItemFilter filter, CancellationToken ct) =>
        db.SyncRunItems.AsNoTracking()
            .Where(x => x.SyncRunId == filter.SyncRunId)
            .WhereIf(filter.Status.HasValue, x => x.Status == filter.Status)
            // Id là thứ tự ổn định, tránh item lặp/mất giữa hai page.
            .OrderBy(x => x.Id)
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);

    public void CreateItems(IEnumerable<SyncRunItem> items) => db.SyncRunItems.AddRange(items);
}
```

`src/YTTrending.Application/Features/Jobs/Dtos/SyncRunItemDto.cs`

```csharp
namespace YTTrending.Application.Features.Jobs.Dtos;

public sealed record SyncRunItemDto(
    int Id,
    int ChannelId,
    string ChannelName,
    SyncRunItemStatus Status,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public static class SyncRunItemMappings
{
    public static SyncRunItemDto ToDto(this SyncRunItem item) => new(
        item.Id, item.ChannelId, item.ChannelName, item.Status,
        item.ErrorCode, item.ErrorMessage, item.StartedAt, item.CompletedAt);
}
```

`src/YTTrending.Application/Features/Jobs/Queries/GetSyncRunItems/GetSyncRunItemsQuery.cs`

```csharp
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRunItems;

public sealed record GetSyncRunItemsQuery
    : SyncRunItemFilter, IRequest<Result<PagedResult<SyncRunItemDto>>>;
```

`src/YTTrending.Application/Features/Jobs/Queries/GetSyncRunItems/GetSyncRunItemsQueryHandler.cs`

```csharp
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRunItems;

public sealed class GetSyncRunItemsQueryHandler(ISyncRunRepository syncRuns)
    : IRequestHandler<GetSyncRunItemsQuery, Result<PagedResult<SyncRunItemDto>>>
{
    public async Task<Result<PagedResult<SyncRunItemDto>>> Handle(GetSyncRunItemsQuery query, CancellationToken ct)
    {
        // Route id sai phải là 404, không phải trang list rỗng khó phân biệt.
        if (await syncRuns.GetSummaryByIdAsync(query.SyncRunId, ct) is null)
        {
            return Result<PagedResult<SyncRunItemDto>>.Failure(
                Error.NotFound(SyncRunErrors.NotFound, "Không tìm thấy lượt sync."));
        }

        var result = await syncRuns.GetItemsPagedAsync(query, ct);
        var items = result.Items.Select(x => x.ToDto()).ToList();
        return Result<PagedResult<SyncRunItemDto>>.Success(
            new PagedResult<SyncRunItemDto>(items, result.Page, result.PageSize, result.TotalCount));
    }
}
```

`src/YTTrending.Application/Features/Jobs/Queries/GetSyncRunItems/GetSyncRunItemsQueryValidator.cs`

```csharp
namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRunItems;

public sealed class GetSyncRunItemsQueryValidator : AbstractValidator<GetSyncRunItemsQuery>
{
    public GetSyncRunItemsQueryValidator() => RuleFor(x => x.SyncRunId).GreaterThan(0);
}
```

**So với bản của Batch 2, thay đổi:**

- POST trả `AcceptedAtAction` vì `GetSyncRun` đã tồn tại;
- thêm `GET /api/jobs/sync/{id}/items`;
- ép `SyncRunId` lấy từ route bằng `with`.

Thay toàn bộ `src/YTTrending.API/Controllers/JobsController.cs`:

```csharp
using YTTrending.Application.Features.Jobs.Commands.CreateSyncRun;
using YTTrending.Application.Features.Jobs.Queries.GetSyncRun;
using YTTrending.Application.Features.Jobs.Queries.GetSyncRunItems;
using YTTrending.Domain.Enums;

namespace YTTrending.API.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(ISender sender) : ControllerBase
{
    [HttpPost("sync")]
    public async Task<IActionResult> CreateSyncRun(CancellationToken ct)
    {
        var result = await sender.Send(new CreateSyncRunCommand(SyncRunTriggerType.Manual), ct);
        if (!result.IsSuccess)
            return result.ToActionResult();

        return AcceptedAtAction(nameof(GetSyncRun), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("sync/{id:int}")]
    public async Task<IActionResult> GetSyncRun(int id, CancellationToken ct)
        => (await sender.Send(new GetSyncRunQuery(id), ct)).ToActionResult();

    [HttpGet("sync/{id:int}/items")]
    public async Task<IActionResult> GetSyncRunItems(
        int id,
        [FromQuery] GetSyncRunItemsQuery query,
        CancellationToken ct)
        // `with` tạo query mới để SyncRunId bắt buộc lấy từ route.
        => (await sender.Send(query with { SyncRunId = id }, ct)).ToActionResult();
}
```

### Nghiệm thu — dừng Batch 3 tại đây

1. Chạy `dotnet build`, API và tạo một run Pending như Batch 2.
2. `GET /api/jobs/sync/{id}/items?page=1&pageSize=20` trả item snapshot theo `id` tăng dần,
   `totalCount = SyncRun.TotalCount` và các item đều Pending.
3. `GET .../items?status=Pending` chỉ trả item Pending; status không có kết quả trả trang rỗng hợp lệ.
4. Id run không tồn tại trả `404 syncRun.notFound`; page vẫn có thứ tự ổn định.
5. Dừng API, giữ lại một run Pending để Batch 4 kiểm tra recovery. Không đăng ký worker ở batch này.

---

## Batch 4 — Worker xử lý run và recovery khi restart

**Một lát chức năng không tách nhỏ hơn được:** consumer queue, processor và recovery phải xuất hiện
cùng nhau. Nếu chỉ thêm worker mà chưa có recovery, các run Pending từ Batch 2/3 bị mất queue khi
restart và mắc kẹt vĩnh viễn; nếu chỉ có processor mà chưa có worker thì không có đường chạy thật.

### Làm

1. Thêm `ProcessSyncRunCommand` + handler. Handler chỉ orchestration: lấy run Pending tracked, mark
   `Running`, đọc item Pending theo Id, gửi `SyncChannelCommand(item.ChannelId)`,
   map outcome vào item/counter, rồi chốt `Completed` hoặc `CompletedWithIssues`.
2. Save ngay sau bốn mốc bền vững: run Running, item Running, item terminal, run terminal.
   Đây là ngoại lệ có chủ ý với quy ước một `SaveChangesAsync`/handler.
3. Quy tắc outcome: `SyncInProgress`, `SyncTooSoon`, `NotFound` là `Skipped`; lỗi Result khác hoặc
   exception khi sync channel là `Failed`. Exception ở cấp processor làm run `Failed` và item chưa
   terminal thành `Skipped` với `syncRun.executionFailed`.
4. Thêm `InterruptIncompleteSyncRunsCommand` + handler. Startup lấy run Pending/Running của process
   cũ, mark chúng `Interrupted`; item Pending/Running thành `Skipped` với `syncRun.interrupted`.
   Không resume.
5. Thêm `SyncRunWorker : BackgroundService`: `StartAsync` chạy recovery trong scope; `ExecuteAsync`
   dequeue run id, tạo scope mới, gửi `ProcessSyncRunCommand`. Worker tự catch/log exception và thoát
   sạch khi stopping token bị hủy.
6. Đăng ký worker sau queue/lock. Cập nhật phần Sync worker/recovery vào
   `docs/domain/background-jobs.md`.

### Được phép sửa/tạo

```text
Application/Features/Jobs/Commands/ProcessSyncRun/*
Application/Features/Jobs/Commands/InterruptIncompleteSyncRuns/*
Application/Features/Jobs/SyncRunErrors.cs
Application/Common/Interfaces/Persistence/ISyncRunRepository.cs
Infrastructure/Jobs/Sync/SyncRunWorker.cs
Infrastructure/Persistence/Repositories/SyncRunRepository.cs
Infrastructure/DependencyInjection.cs
docs/domain/background-jobs.md
ai/current.md
```

### Code copy-paste — Batch 4

**So với bản của Batch 3, chỉ thêm ba method tracked cho worker/recovery:**

- `GetPendingItemsAsync` lấy item Pending của một run;
- `GetIncompleteAsync` lấy run Pending/Running lúc app start;
- `GetUnfinishedItemsAsync` lấy item Pending/Running để chốt state khi lỗi hoặc restart.

Thay toàn bộ `src/YTTrending.Application/Common/Interfaces/Persistence/ISyncRunRepository.cs`:

```csharp
namespace YTTrending.Application.Common.Interfaces.Persistence;

public interface ISyncRunRepository : IRepository<SyncRun>
{
    Task<bool> HasActiveAsync(CancellationToken ct);
    Task<SyncRun?> GetSummaryByIdAsync(int id, CancellationToken ct);
    // Các method dưới trả entity tracked vì worker sẽ chuyển state và save progress.
    Task<List<SyncRunItem>> GetPendingItemsAsync(int syncRunId, CancellationToken ct);
    Task<List<SyncRun>> GetIncompleteAsync(CancellationToken ct);
    Task<List<SyncRunItem>> GetUnfinishedItemsAsync(IReadOnlyList<int> syncRunIds, CancellationToken ct);
    Task<PagedResult<SyncRunItem>> GetItemsPagedAsync(SyncRunItemFilter filter, CancellationToken ct);
    void CreateItems(IEnumerable<SyncRunItem> items);
}
```

**So với bản của Batch 3, chỉ thêm implementation cho ba method worker/recovery ở trên.**
Query item paged, summary và create item giữ nguyên để API không đổi hành vi.

Thay toàn bộ `src/YTTrending.Infrastructure/Persistence/Repositories/SyncRunRepository.cs`:

```csharp
using YTTrending.Application.Common.Extensions;
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Common.Models;
using YTTrending.Domain.Enums;

namespace YTTrending.Infrastructure.Persistence.Repositories;

public sealed class SyncRunRepository(YTTrendingDbContext db)
    : Repository<SyncRun>(db), ISyncRunRepository
{
    public Task<bool> HasActiveAsync(CancellationToken ct) =>
        Set.AnyAsync(x => x.Status == SyncRunStatus.Pending || x.Status == SyncRunStatus.Running, ct);

    public Task<SyncRun?> GetSummaryByIdAsync(int id, CancellationToken ct) =>
        Set.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<List<SyncRunItem>> GetPendingItemsAsync(int syncRunId, CancellationToken ct) =>
        db.SyncRunItems.Where(x => x.SyncRunId == syncRunId && x.Status == SyncRunItemStatus.Pending)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

    public Task<List<SyncRun>> GetIncompleteAsync(CancellationToken ct) =>
        Set.Where(x => x.Status == SyncRunStatus.Pending || x.Status == SyncRunStatus.Running)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

    public Task<List<SyncRunItem>> GetUnfinishedItemsAsync(IReadOnlyList<int> syncRunIds, CancellationToken ct) =>
        db.SyncRunItems.Where(x => syncRunIds.Contains(x.SyncRunId)
                && (x.Status == SyncRunItemStatus.Pending || x.Status == SyncRunItemStatus.Running))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

    public Task<PagedResult<SyncRunItem>> GetItemsPagedAsync(SyncRunItemFilter filter, CancellationToken ct) =>
        db.SyncRunItems.AsNoTracking()
            .Where(x => x.SyncRunId == filter.SyncRunId)
            .WhereIf(filter.Status.HasValue, x => x.Status == filter.Status)
            .OrderBy(x => x.Id)
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);

    public void CreateItems(IEnumerable<SyncRunItem> items) => db.SyncRunItems.AddRange(items);
}
```

**So với bản của Batch 2, chỉ thêm error code execution:** `Interrupted`,
`ChannelExecutionFailed`, `ExecutionFailed`. Ba error code tạo/read run giữ nguyên.

Thay toàn bộ `src/YTTrending.Application/Features/Jobs/SyncRunErrors.cs`:

```csharp
namespace YTTrending.Application.Features.Jobs;

public static class SyncRunErrors
{
    public const string InProgress = "syncRun.inProgress";
    public const string NoEnabledChannels = "syncRun.noEnabledChannels";
    public const string NotFound = "syncRun.notFound";
    public const string Interrupted = "syncRun.interrupted";
    public const string ChannelExecutionFailed = "syncRun.channelExecutionFailed";
    public const string ExecutionFailed = "syncRun.executionFailed";
}
```

`src/YTTrending.Application/Features/Jobs/Commands/ProcessSyncRun/ProcessSyncRunCommand.cs`

```csharp
namespace YTTrending.Application.Features.Jobs.Commands.ProcessSyncRun;

public sealed record ProcessSyncRunCommand(int SyncRunId) : IRequest<Result>;
```

`src/YTTrending.Application/Features/Jobs/Commands/ProcessSyncRun/ProcessSyncRunCommandHandler.cs`

```csharp
using Microsoft.Extensions.Logging;
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.Channels;
using YTTrending.Application.Features.Jobs.Commands.SyncChannel;

namespace YTTrending.Application.Features.Jobs.Commands.ProcessSyncRun;

public sealed class ProcessSyncRunCommandHandler(
    ISyncRunRepository syncRuns,
    IUnitOfWork uow,
    ISender sender,
    TimeProvider clock,
    ILogger<ProcessSyncRunCommandHandler> logger)
    : IRequestHandler<ProcessSyncRunCommand, Result>
{
    public async Task<Result> Handle(ProcessSyncRunCommand command, CancellationToken ct)
    {
        // Tracked entity để processor cập nhật durable progress trên đúng run này.
        var run = await syncRuns.GetByIdAsync(command.SyncRunId, ct);
        // Queue có thể trùng id hoặc run đã recovery; bỏ qua để không chạy lại.
        if (run is null || run.Status != SyncRunStatus.Pending)
            return Result.Success();

        try
        {
            // Save mốc Running ngay để restart biết run đã bắt đầu.
            run.Status = SyncRunStatus.Running;
            run.StartedAt = clock.GetUtcNow();
            await uow.SaveChangesAsync(ct);

            var items = await syncRuns.GetPendingItemsAsync(run.Id, ct);
            foreach (var item in items)
            {
                // Save trước call YouTube để recovery nhận diện item đang dở dang.
                item.Status = SyncRunItemStatus.Running;
                item.StartedAt = clock.GetUtcNow();
                await uow.SaveChangesAsync(ct);

                try
                {
                    // Batch 5 mới truyền trigger; hiện tại command default Manual giữ handler cũ hoạt động.
                    var result = await sender.Send(new SyncChannelCommand(item.ChannelId), ct);
                    CompleteItem(run, item, result.Error);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Sync run {SyncRunId} failed for channel {ChannelId}", run.Id, item.ChannelId);
                    item.Status = SyncRunItemStatus.Failed;
                    item.ErrorCode = SyncRunErrors.ChannelExecutionFailed;
                    item.ErrorMessage = exception.Message;
                    run.FailedCount++;
                }

                // Item terminal + counter được save từng vòng, không gom đến cuối run.
                item.CompletedAt = clock.GetUtcNow();
                await uow.SaveChangesAsync(ct);
            }

            run.Status = run.FailedCount > 0 || run.SkippedCount > 0
                ? SyncRunStatus.CompletedWithIssues
                : SyncRunStatus.Completed;
            run.CompletedAt = clock.GetUtcNow();
            await uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Processor lỗi: mọi item chưa terminal đều được chốt Skipped để progress không bị kẹt.
            var unfinishedItems = await syncRuns.GetUnfinishedItemsAsync([run.Id], ct);
            var now = clock.GetUtcNow();
            foreach (var item in unfinishedItems)
            {
                item.Status = SyncRunItemStatus.Skipped;
                item.ErrorCode = SyncRunErrors.ExecutionFailed;
                item.ErrorMessage = "Không thể hoàn tất lượt sync.";
                item.CompletedAt = now;
            }

            run.Status = SyncRunStatus.Failed;
            run.SkippedCount += unfinishedItems.Count;
            run.ErrorCode = SyncRunErrors.ExecutionFailed;
            run.ErrorMessage = "Sync run could not be completed.";
            run.CompletedAt = now;
            await uow.SaveChangesAsync(ct);
            throw;
        }
    }

    private static void CompleteItem(SyncRun run, SyncRunItem item, Error? error)
    {
        if (error is null)
        {
            item.Status = SyncRunItemStatus.Succeeded;
            run.SuccessCount++;
            return;
        }

        // Lỗi nghiệp vụ dự kiến bỏ qua item; exception hạ tầng là Failed.
        if (error.Code is ChannelErrors.SyncInProgress or ChannelErrors.SyncTooSoon or ChannelErrors.NotFound)
        {
            item.Status = SyncRunItemStatus.Skipped;
            run.SkippedCount++;
        }
        else
        {
            item.Status = SyncRunItemStatus.Failed;
            run.FailedCount++;
        }

        item.ErrorCode = error.Code;
        item.ErrorMessage = error.Message;
    }
}
```

`src/YTTrending.Application/Features/Jobs/Commands/InterruptIncompleteSyncRuns/InterruptIncompleteSyncRunsCommand.cs`

```csharp
namespace YTTrending.Application.Features.Jobs.Commands.InterruptIncompleteSyncRuns;

public sealed record InterruptIncompleteSyncRunsCommand : IRequest<Result>;
```

`src/YTTrending.Application/Features/Jobs/Commands/InterruptIncompleteSyncRuns/InterruptIncompleteSyncRunsCommandHandler.cs`

```csharp
using YTTrending.Application.Common.Interfaces;

namespace YTTrending.Application.Features.Jobs.Commands.InterruptIncompleteSyncRuns;

public sealed class InterruptIncompleteSyncRunsCommandHandler(
    ISyncRunRepository syncRuns,
    IUnitOfWork uow,
    TimeProvider clock)
    : IRequestHandler<InterruptIncompleteSyncRunsCommand, Result>
{
    public async Task<Result> Handle(InterruptIncompleteSyncRunsCommand command, CancellationToken ct)
    {
        var runs = await syncRuns.GetIncompleteAsync(ct);
        if (runs.Count == 0)
            return Result.Success();

        var runIds = runs.Select(x => x.Id).ToList();
        var items = await syncRuns.GetUnfinishedItemsAsync(runIds, ct);
        var now = clock.GetUtcNow();

        // Không resume: crash có thể xảy ra sau channel save nhưng trước item được mark success.
        foreach (var item in items)
        {
            item.Status = SyncRunItemStatus.Skipped;
            item.ErrorCode = SyncRunErrors.Interrupted;
            item.ErrorMessage = "Ứng dụng dừng trước khi channel được xử lý xong.";
            item.CompletedAt = now;
        }

        foreach (var run in runs)
        {
            run.Status = SyncRunStatus.Interrupted;
            run.SkippedCount += items.Count(x => x.SyncRunId == run.Id);
            run.ErrorCode = SyncRunErrors.Interrupted;
            run.ErrorMessage = "Ứng dụng dừng trước khi lượt sync hoàn tất.";
            run.CompletedAt = now;
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

`src/YTTrending.Infrastructure/Jobs/Sync/SyncRunWorker.cs`

```csharp
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.Jobs.Commands.InterruptIncompleteSyncRuns;
using YTTrending.Application.Features.Jobs.Commands.ProcessSyncRun;

namespace YTTrending.Infrastructure.Jobs.Sync;

public sealed class SyncRunWorker(
    IServiceScopeFactory scopeFactory,
    ISyncRunQueue queue,
    ILogger<SyncRunWorker> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken ct)
    {
        // Chốt run cũ trước khi worker nhận run mới; recovery không resume.
        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new InterruptIncompleteSyncRunsCommand(), ct);
        await base.StartAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var syncRunId = await queue.DequeueAsync(stoppingToken);
                // Hosted service singleton nên mỗi run cần scope mới cho ISender/DbContext scoped.
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new ProcessSyncRunCommand(syncRunId), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Sync run worker failed while processing a queued run");
            }
        }
    }
}
```

**So với bản của Batch 2, chỉ thêm:** `services.AddHostedService<SyncRunWorker>()` sau queue/lock.
Các lifetime repository, queue và lock không đổi.

Thay toàn bộ `src/YTTrending.Infrastructure/DependencyInjection.cs`:

```csharp
using YTTrending.Application.Common.Interfaces;
using YTTrending.Infrastructure.Concurrency;
using YTTrending.Infrastructure.Jobs.Sync;
using YTTrending.Infrastructure.Persistence;
using YTTrending.Infrastructure.Persistence.Repositories;
using YTTrending.Infrastructure.YouTube;

namespace YTTrending.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<YTTrendingDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("Default"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<ISyncRunRepository, SyncRunRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IChannelSyncLock, ChannelSyncLock>();
        services.AddSingleton<ISyncRunQueue, SyncRunQueue>();
        services.AddSingleton<ISyncRunCreationLock, SyncRunCreationLock>();
        services.AddHostedService<SyncRunWorker>();

        if (configuration.GetValue("YouTube:UseFake", true))
        {
            services.AddSingleton<IYouTubeClient, FakeYouTubeClient>();
        }
        else
        {
            services.AddHttpClient<IYouTubeClient, YouTubeClient>(client =>
            {
                client.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/");
            });
        }

        return services;
    }
}
```

### Nghiệm thu — dừng Batch 4 tại đây

1. Chạy `dotnet build`, sau đó khởi động API với run Pending được cố ý giữ từ Batch 2/3.
2. Ngay sau startup, GET run cũ trả `Interrupted`; item chưa xong là `Skipped`, code
   `syncRun.interrupted`, `processedCount = totalCount`.
3. Tạo một run mới qua POST. Poll GET summary đến terminal; fake YouTube có thể khiến nó Completed
   ngay. Ở trạng thái terminal: `success + skipped + failed = processed = total`.
4. `GET items?status=Failed` chỉ trả item Failed và error riêng của channel; single-channel endpoint
   cũ vẫn hoạt động độc lập.

---

## Batch 5 — Cooldown đúng theo trigger

**Một việc duy nhất:** làm rõ khác biệt manual/scheduled trong sync **một** channel. Chưa có timer
scheduler mới.

### Làm

1. Thêm `SyncRunTriggerType TriggerType = Manual` vào `SyncChannelCommand`; default giữ nguyên
   contract của `POST /api/channels/{id}/sync`.
2. `ValidateSyncInterval` nhận trigger: Manual dùng `ManualSyncCooldownHours`, Scheduled dùng
   `SyncIntervalHours`.
3. Đổi đúng một call site ở `ProcessSyncRunCommandHandler` để worker truyền trigger của run vào
   sync một channel. Giữ lock, discovery và video lifecycle nguyên vẹn.

### Được phép sửa

```text
src/YTTrending.Application/Features/Jobs/Commands/SyncChannel/SyncChannelCommand.cs
src/YTTrending.Application/Features/Jobs/Commands/SyncChannel/SyncChannelCommandHandler.cs
src/YTTrending.Application/Features/Jobs/Commands/ProcessSyncRun/ProcessSyncRunCommandHandler.cs
ai/current.md
```

### Code copy-paste — Batch 5

**So với file cũ, chỉ thêm:** tham số `TriggerType` có default `Manual`. Vì có default nên mọi call
site/controller cũ truyền một `ChannelId` vẫn compile và giữ đúng hành vi.

Thay toàn bộ `src/YTTrending.Application/Features/Jobs/Commands/SyncChannel/SyncChannelCommand.cs`:

```csharp
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Commands.SyncChannel;

public sealed record SyncChannelCommand(
    int ChannelId,
    // Default giữ controller single-channel hiện tại là Manual mà không đổi API contract.
    SyncRunTriggerType TriggerType = SyncRunTriggerType.Manual)
    : IRequest<Result<SyncChannelResultDto>>;
```

**So với file cũ, thay đổi duy nhất trong logic:** `ValidateSyncInterval` nhận `TriggerType` và chọn
cooldown tương ứng. Lock, lấy playlist, discovery, video lifecycle, Result và một lần save vẫn giữ
nguyên.

Thay toàn bộ `src/YTTrending.Application/Features/Jobs/Commands/SyncChannel/SyncChannelCommandHandler.cs`:

```csharp
using Microsoft.Extensions.Options;
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Common.Options;
using YTTrending.Application.Common.Services;
using YTTrending.Application.Features.Channels;
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Commands.SyncChannel;

public sealed class SyncChannelCommandHandler(
    IUnitOfWork uow,
    IChannelRepository channels,
    IYouTubeClient youtube,
    IChannelSyncLock syncLock,
    IShortsDiscoveryService shortsDiscovery,
    IVideoSyncService videoSync,
    IOptionsMonitor<TrackingOptions> trackingOptions,
    TimeProvider clock)
    : IRequestHandler<SyncChannelCommand, Result<SyncChannelResultDto>>
{
    public async Task<Result<SyncChannelResultDto>> Handle(SyncChannelCommand cmd, CancellationToken ct)
    {
        // Chặn sync chồng trên cùng một channel nhưng không chặn channel khác.
        if (!syncLock.TryAcquire(cmd.ChannelId))
            return Result<SyncChannelResultDto>.Failure(Error.Conflict(ChannelErrors.SyncInProgress, "Channel đang được sync, thử lại sau."));

        try
        {
            // Snapshot config cho trọn lượt sync, tránh hot reload đổi giữa các bước.
            var tracking = trackingOptions.CurrentValue;
            var channel = await channels.GetByIdAsync(cmd.ChannelId, ct);
            if (channel is null)
                return Result<SyncChannelResultDto>.Failure(Error.NotFound(ChannelErrors.NotFound, "Không tìm channel với id được cung cấp."));

            // Một mốc UTC chung cho cooldown, discovery và lifecycle của lượt này.
            var now = clock.GetUtcNow();
            var syncIntervalError = ValidateSyncInterval(channel, now, tracking, cmd.TriggerType);
            if (syncIntervalError is not null)
                return Result<SyncChannelResultDto>.Failure(syncIntervalError);

            var uploadsPlaylistResult = await EnsureUploadsPlaylistIdAsync(channel, ct);
            if (!uploadsPlaylistResult.IsSuccess)
                return Result<SyncChannelResultDto>.Failure(uploadsPlaylistResult.Error!);

            var discovery = await shortsDiscovery.DiscoverAsync(uploadsPlaylistResult.Value, now, tracking, ct);
            var videoChanges = await videoSync.ApplyDiscoveryAsync(channel, discovery.SelectedShorts, now, tracking, ct);

            channel.LastSyncAt = now;
            await uow.SaveChangesAsync(ct);
            return Result<SyncChannelResultDto>.Success(new(
                FetchedShortsCount: discovery.FetchedShortsCount,
                QualifiedShortsCount: discovery.QualifiedShortsCount,
                NewlyDiscoveredCount: videoChanges.NewlyDiscoveredCount,
                NewlyTrackedCount: videoChanges.NewlyTrackedCount,
                ExistingVideosRefreshedCount: videoChanges.ExistingVideosRefreshedCount,
                ArchivedVideosCount: videoChanges.ArchivedVideosCount));
        }
        finally
        {
            syncLock.Release(cmd.ChannelId);
        }
    }

    private static Error? ValidateSyncInterval(
        Channel channel,
        DateTimeOffset now,
        TrackingOptions tracking,
        SyncRunTriggerType triggerType)
    {
        // Manual là hành động chủ đích nên có cooldown riêng; scheduler theo chu kỳ SyncIntervalHours.
        var cooldown = triggerType == SyncRunTriggerType.Manual
            ? TimeSpan.FromHours(tracking.ManualSyncCooldownHours)
            : TimeSpan.FromHours(tracking.SyncIntervalHours);

        if (channel.LastSyncAt is not { } lastSync || now - lastSync >= cooldown)
            return null;

        return Error.Conflict(ChannelErrors.SyncTooSoon,
            $"Channel vừa sync lúc {lastSync:HH:mm dd/MM}, thử lại sau.");
    }

    private async Task<Result<string>> EnsureUploadsPlaylistIdAsync(Channel channel, CancellationToken ct)
    {
        // Uploads playlist ổn định theo channel, cache lại để tránh gọi YouTube lại ở các lượt sau.
        if (channel.UploadsPlaylistId is not null)
            return Result<string>.Success(channel.UploadsPlaylistId);

        channel.UploadsPlaylistId = await youtube.GetUploadsPlaylistIdAsync(channel.YoutubeChannelId, ct);
        return channel.UploadsPlaylistId is null
            ? Result<string>.Failure(Error.NotFound(ChannelErrors.NotFound, "Channel không còn tồn tại trên YouTube."))
            : Result<string>.Success(channel.UploadsPlaylistId);
    }
}
```

**So với bản Batch 4, chỉ thay đúng call site này:** truyền trigger đã lưu trên run cho channel đang
được worker xử lý.

Trong `ProcessSyncRunCommandHandler`, thay đúng call gửi command (bên trong `foreach`):

```csharp
var result = await sender.Send(
    new SyncChannelCommand(item.ChannelId, run.TriggerType), ct);
```

### Nghiệm thu — dừng Batch 5 tại đây

1. `dotnet build` pass.
2. Swagger `POST /api/channels/{id}/sync` vẫn gửi được command default Manual và áp dụng manual
   cooldown như trước.
3. Review call site: chỉ worker/scheduler tương lai mới truyền `Scheduled`; controller single-channel
   không phải đổi request/response.

---

## Batch 6 — Scheduler chỉ tạo scheduled run

**Một việc duy nhất:** thêm đồng hồ định kỳ gọi use case tạo run đã có. Nó không chứa loop channel,
YouTube call hay logic progress.

### Làm

1. Thêm `SyncChannelJob : BackgroundService`; mỗi vòng tạo `PeriodicTimer` từ
   `TrackingOptions.SyncIntervalHours`, đợi tick rồi tạo scope và gửi
   `CreateSyncRunCommand(Scheduled)`.
2. Sau mỗi tick tạo timer mới để hot reload interval có hiệu lực ở chu kỳ kế tiếp. Không “run ngay
   khi app start”.
3. `Jobs:SyncEnabled = false` chỉ log và bỏ tick, không chặn manual endpoint. `syncRun.inProgress`
   chỉ log/bỏ tick; exception ngoài dự kiến tự catch/log.
4. Đăng ký job **sau** `SyncRunWorker`. Cập nhật scheduler trong `docs/domain/background-jobs.md`;
   cập nhật `docs/config.md` chỉ khi contract config thực tế đổi.

### Được phép sửa/tạo

```text
src/YTTrending.Infrastructure/Jobs/Sync/SyncChannelJob.cs
src/YTTrending.Infrastructure/DependencyInjection.cs
docs/domain/background-jobs.md
docs/config.md                 # chỉ khi config contract thực tế đổi
ai/current.md
```

### Code copy-paste — Batch 6

`src/YTTrending.Infrastructure/Jobs/Sync/SyncChannelJob.cs`

```csharp
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YTTrending.Application.Common.Options;
using YTTrending.Application.Features.Jobs;
using YTTrending.Application.Features.Jobs.Commands.CreateSyncRun;
using YTTrending.Domain.Enums;

namespace YTTrending.Infrastructure.Jobs.Sync;

public sealed class SyncChannelJob(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<JobOptions> jobOptions,
    IOptionsMonitor<TrackingOptions> trackingOptions,
    ILogger<SyncChannelJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Tạo timer lại sau mỗi tick để hot-reload interval có hiệu lực ở chu kỳ kế tiếp.
                using var timer = new PeriodicTimer(
                    TimeSpan.FromHours(trackingOptions.CurrentValue.SyncIntervalHours));
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;

                if (!jobOptions.CurrentValue.SyncEnabled)
                {
                    logger.LogInformation("Scheduled sync tick skipped because Jobs:SyncEnabled is false");
                    continue;
                }

                // Hosted service là singleton; scope cấp ISender/DbContext scoped cho tick này.
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var result = await sender.Send(
                    new CreateSyncRunCommand(SyncRunTriggerType.Scheduled), stoppingToken);

                if (!result.IsSuccess && result.Error?.Code == SyncRunErrors.InProgress)
                    logger.LogInformation("Scheduled sync tick skipped because a sync run is active");
                else if (!result.IsSuccess)
                    logger.LogWarning("Scheduled sync run was not created: {ErrorCode}", result.Error?.Code);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled sync tick failed");
            }
        }
    }
}
```

**So với bản của Batch 4, chỉ thêm:** `services.AddHostedService<SyncChannelJob>()` ngay sau
`SyncRunWorker`. Không đổi lifetime/registration nào khác.

Thay toàn bộ `src/YTTrending.Infrastructure/DependencyInjection.cs`:

```csharp
using YTTrending.Application.Common.Interfaces;
using YTTrending.Infrastructure.Concurrency;
using YTTrending.Infrastructure.Jobs.Sync;
using YTTrending.Infrastructure.Persistence;
using YTTrending.Infrastructure.Persistence.Repositories;
using YTTrending.Infrastructure.YouTube;

namespace YTTrending.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<YTTrendingDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("Default"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<IVideoRepository, VideoRepository>();
        services.AddScoped<ISyncRunRepository, SyncRunRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IChannelSyncLock, ChannelSyncLock>();
        services.AddSingleton<ISyncRunQueue, SyncRunQueue>();
        services.AddSingleton<ISyncRunCreationLock, SyncRunCreationLock>();
        services.AddHostedService<SyncRunWorker>();
        // Scheduler chỉ tạo run; worker ở trên mới là nơi xử lý item/channel.
        services.AddHostedService<SyncChannelJob>();

        if (configuration.GetValue("YouTube:UseFake", true))
        {
            services.AddSingleton<IYouTubeClient, FakeYouTubeClient>();
        }
        else
        {
            services.AddHttpClient<IYouTubeClient, YouTubeClient>(client =>
            {
                client.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/");
            });
        }

        return services;
    }
}
```

### Nghiệm thu — dừng Batch 6 tại đây

1. `dotnet build` pass.
2. Với `SyncEnabled = false`, chờ qua một tick: không có scheduled run; manual POST vẫn tạo được run.
3. Với `SyncEnabled = true`, sau tick kế tiếp có run `triggerType = Scheduled` và nó được worker xử
   lý; khi có run active, tick chỉ log, không tạo run thứ hai.
4. `SyncIntervalHours` có min 1, nên manual scheduler verification cần chờ tối đa một giờ. Ghi rõ
   kết quả hoặc khoản nợ verify trong `ai/current.md`, rồi trả config về giá trị mong muốn.

---

## Batch 7 — Đóng tài liệu và trạng thái

Chỉ bắt đầu khi Batch 1–6 đều đã pass nghiệm thu. Đây là batch tài liệu, không thay đổi hành vi.

### Làm và nghiệm thu

1. Đối chiếu `docs/database.md` với migration thực tế: hai bảng, nullable, length, index, cascade
   và lý do `ChannelId` không FK.
2. Đối chiếu `docs/api-contract.md` với controller/DTO thực tế: ba route, POST 202, enum string,
   pagination và toàn bộ `syncRun.*` error code.
3. Đối chiếu `docs/domain/background-jobs.md` với worker/scheduler thật: durable progress, no-resume
   recovery, queue chỉ giữ run id, scheduled trigger và Metrics Update vẫn độc lập.
4. Cập nhật `ai/current.md`/`ai/history.md` bằng batch hoàn thành, migration, lệnh đã chạy, kết quả
   API/migration và nợ scheduler (nếu vẫn phải chờ tick).
5. Chạy `dotnet build` lần cuối. Chỉ khi mọi mục đều hoàn tất mới xoá plan tạm này và chỉnh pointer
   từ plan cũ nếu còn.

## Thứ tự không được đảo

```text
1 Schema + migration
  → 2 Tạo/Poll summary Pending
    → 3 List item paged
      → 4 Worker + recovery end-to-end
        → 5 Trigger-aware cooldown
          → 6 Scheduled creator
            → 7 Docs closeout
```

Mỗi mũi tên là dependency thật: batch sau dùng kết quả đã nghiệm thu của batch trước. Không có batch
nào chỉ “viết sẵn code chờ sau này”; ngoại lệ duy nhất là Batch 4, nơi worker, processor và recovery
phải đi cùng để không sinh run mắc kẹt sau restart.
