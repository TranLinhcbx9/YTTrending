# Setup Base — Ghi chú chi tiết

> **Không phải checklist.** Checklist gạch đầu dòng ở [`setup-base.md`](setup-base.md) — file này là phần tra cứu: cách làm, code mẫu, lý do chọn, cạm bẫy đã biết.
> Đọc khi bắt tay vào một mục cụ thể, không đọc từ đầu đến cuối.
> Kiến trúc gốc: [`../docs/architecture.md`](../docs/architecture.md). Quyết định đã chốt: [`../docs/decisions.md`](../docs/decisions.md).

## Phần A — Base này nên có gì (ngoài những gì docs đã viết)

Docs đã chốt kiến trúc lớn. Dưới đây là những thứ nhỏ nhưng nếu không làm ngay từ đầu thì sau phải sửa rải rác khắp nơi.

### A1. Central Package Management (`Directory.Packages.props`)

Version NuGet khai một chỗ, csproj chỉ ghi tên package. Với dự án này nó chặn đúng một lỗi cụ thể: **MediatR tự nhảy lên 13.x** (license thương mại — [`../docs/decisions.md`](../docs/decisions.md)). Khai `<PackageVersion Include="MediatR" Version="12.4.1" />` một chỗ thì không có đường trượt.

### A2. `Directory.Build.props` — bật `TreatWarningsAsErrors` cho nullable

`Nullable` đã bật nhưng warning vẫn chỉ là warning. Navigation property phải khai `= null!` (EF chỉ gán khi `Include`) — chỗ này rất dễ đẻ ra `null!` rải rác. Bật warning-as-error ngay từ khi repo còn trống thì rẻ; bật sau khi có 50 file thì không ai bật nữa.

### A3. Naming convention snake_case ⚠️

[`../docs/database.md`](../docs/database.md) viết schema kiểu `youtube_channel_id`, `last_sync_at`. EF Core mặc định sinh ra `YoutubeChannelId`, `LastSyncAt`. **Không khớp.**

Giải: thêm package `EFCore.NamingConventions` + `.UseSnakeCaseNamingConvention()` khi đăng ký DbContext. Một dòng, và không phải viết `HasColumnName` cho từng property.

Phải quyết **trước khi tạo migration đầu tiên** — đổi sau nghĩa là drop DB làm lại.

### A4. Audit fields tập trung, không set tay

**Vấn đề:** `created_at` / `updated_at` là thứ mọi handler đều phải nhớ set. Quên một chỗ thì không có gì báo — chỉ phát hiện lúc nhìn dữ liệu thấy `updated_at` đứng im dù record đã sửa.

**Giải:** đánh dấu entity nào cần audit, rồi để **một** chỗ điền tự động lúc `SaveChanges`.

#### (1) Entity nào cần audit — không phải tất cả

Đọc kỹ [`../docs/database.md`](../docs/database.md) thì 5 bảng chia làm 2 nhóm khác hẳn nhau:

| Bảng | Cột thời gian | Loại |
|---|---|---|
| `channels` | `created_at`, `updated_at` | **Audit** — metadata kỹ thuật |
| `videos` | `created_at`, `updated_at` | **Audit** — metadata kỹ thuật |
| `videos` | `archived_at` | **Dữ liệu nghiệp vụ** — do `VideoStateRules.Archive()` set, là đồng hồ đếm retention |
| `saved_ideas` | `created_at`, `updated_at` | **Audit** — `note` sửa được (`UpdateNote`) nên `updated_at` mang thông tin thật |
| `video_metric_snapshots` | `snapshot_at` | **Dữ liệu nghiệp vụ** — là *nội dung* của record |
| `trending_scores` | `calculated_at` | **Dữ liệu nghiệp vụ** |

Ranh giới cần nhớ: **audit thì tự động, thời gian nghiệp vụ thì set tường minh.** `snapshot_at` trả lời câu hỏi "số liệu này đo lúc nào" — nó là dữ liệu, có thể lệch thời điểm insert row, nên phải nhìn thấy trong code chỗ tạo snapshot, không được để một interceptor ngầm điền hộ.

Ngược lại `saved_ideas.created_at` đúng nghĩa là "row này sinh lúc nào" (bookmark = insert, không lệch được), nên để audit điền là chuẩn.

✅ **Đã chốt: `Channel`, `Video`, `SavedIdea` kế thừa `AuditableEntity`.** Không tạo interface `IHasCreatedAt` — thêm một vòng lặp nữa trong `ApplyAuditFields()` để phục vụ đúng 1 entity thì không chặn được lỗi gì, mà `saved_ideas` có `updated_at` là hợp lý sẵn (note sửa được). Hai entity còn lại — `VideoMetricSnapshot`, `TrendingScore` — **không** kế thừa: chúng chỉ mang thời gian nghiệp vụ.

#### (2) Base class + override trong `YTTrendingDbContext` (đã chốt: cách A)

```csharp
// Domain/Common/AuditableEntity.cs
public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
}
```

`private set` để giữ đúng nguyên tắc Domain (không cho object initializer set bừa). Vẫn ghi được, nhưng phải ghi **qua `ChangeTracker` chứ không qua property**:

```csharp
// Infrastructure/Persistence/YTTrendingDbContext.cs
public class YTTrendingDbContext(DbContextOptions<YTTrendingDbContext> options, TimeProvider clock)
    : DbContext(options), IYTTrendingDbContext
{
    // ... DbSet<T> ...

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(ct);
    }

    // BẮT BUỘC override cả bản sync — chỗ nào lỡ gọi SaveChanges() thì audit vẫn chạy
    public override int SaveChanges()
    {
        ApplyAuditFields();
        return base.SaveChanges();
    }

    private void ApplyAuditFields()
    {
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

`entry.Property(...).CurrentValue = now` ghi qua backing field của EF nên **không cần public setter** — đây là cách hoà giải giữa "Domain kín" và "audit tự động".

#### (3) Đăng ký

Không cần gì thêm ngoài việc `TimeProvider` đã có trong DI — EF tự resolve tham số thứ hai của constructor:

```csharp
services.AddSingleton(TimeProvider.System);
services.AddDbContext<YTTrendingDbContext>(o => o
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());
```

`dotnet ef migrations add` cũng chạy bình thường: nó lấy `YTTrendingDbContext` qua service provider của `Program.cs`, mà `TimeProvider` đã đăng ký ở đó.

#### (4) Vì sao override, không dùng `SaveChangesInterceptor`

EF Core có API interception (đăng ký một object vào `DbContextOptions`, EF gọi tại các mốc `SavingChanges` / `SavedChanges` / `SaveChangesFailed`). Nó **không phải** thứ thay thế `ChangeTracker` — interceptor vẫn phải đọc `ChangeTracker` y hệt đoạn trên. Khác biệt duy nhất là đoạn code đó nằm ở file nào.

Chọn override vì đúng nguyên tắc của dự án ([`../docs/architecture.md`](../docs/architecture.md#L7)): *lớp trừu tượng nào không trả lời được "nó chặn được lỗi gì" thì bỏ*. Với **một** DbContext và **một** mối quan tâm lúc save, interceptor không chặn thêm lỗi nào — chỉ thêm 1 file và 2 dòng đăng ký, lại khó tìm hơn (mở `YTTrendingDbContext` không thấy audit đâu). Cùng logic đã dùng để bỏ Repository.

**Khi nào cắt sang interceptor:** khi xuất hiện mối quan tâm **thứ hai** lúc save — soft-delete tự động, domain events, outbox. Lúc đó mỗi thứ một class thay vì một method phình dần. Chuyển tốn ~10 phút vì thân hàm bê nguyên; interceptor còn có sẵn hook `SavedChanges`/`SaveChangesFailed` mà override phải tự try-catch.

#### (5) ⚠️ Ba cạm bẫy

**a) `ExecuteUpdateAsync` / `ExecuteDeleteAsync` không đi qua `SaveChanges`.** Nó dịch thẳng ra một câu SQL, không nạp entity, không có gì trong ChangeTracker để duyệt — nên `ApplyAuditFields()` không bao giờ chạy (interceptor cũng vậy, không phải nhược điểm của cách A). Cleanup Job soft-delete hàng loạt bằng đúng cái này ([`../docs/architecture.md`](../docs/architecture.md#L242)) → phải tự set `updated_at`:

```csharp
await db.Videos
    .Where(v => v.Status == VideoStatus.Archived && v.ArchivedAt < cutoff)
    .ExecuteUpdateAsync(s => s
        .SetProperty(v => v.DeletedAt, now)
        .SetProperty(v => v.UpdatedAt, now), ct);   // ← quên dòng này là audit sai
```

> ⚠️ Chú ý điều kiện `v.ArchivedAt < cutoff` — **không được** dùng `v.UpdatedAt < cutoff`. `updated_at` bị đẩy lại mỗi khi Sync Job sửa title/thumbnail, nên nếu lấy nó làm đồng hồ đếm retention thì một video ARCHIVED bị đổi tiêu đề sẽ **không bao giờ đủ 30 ngày để bị dọn**. Cần cột `archived_at` riêng — xem [`../docs/database.md`](../docs/database.md).

**b) Đừng đặt thêm default ở DB.** Cám dỗ là thêm `HasDefaultValueSql("now()")` cho chắc. Làm vậy thành **hai nguồn thời gian**: code dùng `TimeProvider` (tua được khi debug), DB dùng `now()` của server (không tua được) — và khi nhìn một hàng dữ liệu thì không biết cột đang mang giờ của ai. Tệ hơn: DB default chỉ nhảy vào khi code **không** gửi giá trị, nên nếu `ApplyAuditFields()` sót một đường ghi nào đó, cột vẫn có giá trị trông rất hợp lý — lỗi bị **che đi** thay vì lộ ra. Chọn một nguồn: `TimeProvider`.

**c) Cẩn thận với `db.Update(entity)` — nó làm `updated_at` nhảy dù không có gì đổi.** EF theo dõi thay đổi bằng cách so sánh với bản chụp lúc load, nên nếu sửa entity đang được track mà gán lại đúng giá trị cũ thì **không** bị đánh dấu `Modified` — chỗ này an toàn. Nhưng `db.Update(entity)` (hoặc gán tay `entry.State = EntityState.Modified`) thì đánh dấu **toàn bộ** property là đã đổi, bất kể giá trị có khác hay không.

Đúng chỗ dễ dính trong dự án này: Sync Job kéo về ~100 video/kênh rồi cập nhật title/thumbnail. Nếu viết kiểu `db.Videos.Update(video)` cho từng cái thì **mỗi lần sync là toàn bộ video đổi `updated_at`**, dù YouTube chẳng sửa gì. Cách đúng: load entity ra, gán property (hoặc gọi method domain), rồi `SaveChangesAsync` — để EF tự so sánh và chỉ UPDATE cái nào thật sự khác.

#### (6) Kiểu dữ liệu

Dùng `DateTimeOffset`, không `DateTime`. Lý do: `TimeProvider.GetUtcNow()` trả về `DateTimeOffset`, Npgsql map thẳng sang `timestamptz` ([`../docs/database.md`](../docs/database.md)), và không bao giờ phải đoán "cái `DateTime` này Kind là gì". Toàn hệ thống dùng một kiểu — kể cả `snapshot_at`, `calculated_at`, `published_at`.

### A5. Kill-switch cho background job ở môi trường dev 🔑

YouTube Data API có quota và **quota không hồi lại khi retry**. Nếu job tự chạy mỗi lần bấm F5 thì đốt quota vào việc debug.

Giải: `"Jobs": { "Enabled": false }` trong `appsettings.Development.json`, `BackgroundService` đọc cờ này và `return` ngay nếu tắt. Kèm theo đó là endpoint `POST /api/jobs/sync` để chạy tay đúng lúc mình muốn — cũng chính là cách test job mà không chờ timer (đã ngụ ý ở [`../docs/architecture.md`](../docs/architecture.md#L270)).

### A6. `FakeYouTubeClient` cho dev

`IYouTubeClient` tồn tại để test mock được. Tận dụng luôn cho dev: đăng ký theo config `"YouTube": { "UseFake": true }` → chạy được toàn bộ luồng end-to-end trước khi có API key, không tốn quota nào. Đây là thứ cho phép nghiệm thu tiêu chí #2 mà chưa cần đụng YouTube.

### A7. Secrets không nằm trong repo

API key YouTube + connection string → `dotnet user-secrets` (đã có sẵn cơ chế, không cần package). `appsettings.json` chỉ giữ placeholder rỗng. Repo là public hay không cũng không nên commit key.

> **Ngoại lệ có chủ ý (mục 4): connection string Development để thẳng trong `appsettings.Development.json`.** DB là Postgres local, password chỉ dùng trên đúng một máy, không mở ra ngoài — mất nó không mất gì. Đánh đổi lấy việc `dotnet ef` / PMC / F5 đều đọc được cùng một chỗ, không phải nhớ `dotnet user-secrets set` mỗi lần clone. `appsettings.json` (bản không-Development) **vẫn** giữ placeholder rỗng. Ngoại lệ này **không áp cho YouTube API key** — key đó có quota và gắn với Google account, vẫn phải đi qua user-secrets ở mục 5.

### A8. Postgres cài sẵn trên máy — tạo DB riêng cho dự án

Đã có Postgres local, không cần Docker. Chỉ cần tạo database riêng (`yttrending_dev`) thay vì dùng chung `postgres`, để lúc migration hỏng thì `DROP DATABASE` làm lại mà không đụng dữ liệu dự án khác.

Nên tạo luôn `yttrending_test` nếu sau này muốn có test chạy trên Postgres thật — Phase 1 test dùng Sqlite nên chưa cần.

### A9. `GlobalUsings.cs` mỗi project

`MediatR`, `Microsoft.EntityFrameworkCore`, `YTTrending.Domain.Entities` xuất hiện ở gần như mọi file. Gom vào 1 file mỗi project, header file sạch hẳn.

### A10. Ghi log ra file cho job chạy đêm

Job chạy lúc 3h sáng, console log biến mất khi tắt máy. Cần log lại được "sync lúc nào, bao nhiêu video mới, lỗi gì" để sáng đọc.

**Đã chốt: Serilog + file sink.** `Serilog.AspNetCore` + `Serilog.Sinks.File`, cấu hình bằng section `Serilog` trong `appsettings.json`, rolling theo ngày, giữ ~7 ngày. Thư mục `logs/` nhớ cho vào `.gitignore`.

### A11. Giữ mapping đơn giản, tránh thứ đặc thù Npgsql khi không cần

Cột `status` lưu **varchar + `HasConversion<string>()`**, không dùng native Postgres ENUM. Lý do chính (vẫn đúng dù chưa viết test): thêm một giá trị enum vào native ENUM ở Postgres phải viết migration `ALTER TYPE` thủ công — EF không tự sinh. Lý do phụ: khi nào làm test bằng Sqlite in-memory, mapping đặc thù Npgsql (native enum, `jsonb`, `tsvector`) sẽ làm bước tạo schema vỡ; tránh sẵn thì sau này không phải sửa lại configuration.

Chỗ này [`../docs/database.md`](../docs/database.md) đang ghi `ENUM(...)` còn [`../docs/architecture.md`](../docs/architecture.md#L234) ghi `HasConversion<string>()`. **Đã chốt: varchar + `HasConversion<string>()`** — sửa lại `database.md` cho khớp.

### A12. CORS cho Angular dev server

FE là Angular riêng (20+), chạy ở `http://localhost:4200` — khác origin với API nên **mọi request sẽ bị chặn** nếu không khai CORS. Khai một named policy trong `Program.cs`, origin đọc từ config (`"Cors": { "AllowedOrigins": [...] }`) chứ không hardcode, và **chỉ bật ở Development** cho tới khi biết FE deploy ở đâu.

Hai thứ đi kèm, làm luôn ở base cho FE đỡ phải đoán:
- **Response luôn là JSON có hình dạng cố định** — lỗi trả về đúng `Error { code, type, message }` như `ResultExtensions` đã định nghĩa, để FE viết một interceptor xử lý là xong.
- **`camelCase` JSON** (mặc định của ASP.NET Core, chỉ cần đừng đổi) — khớp thẳng với interface TypeScript bên Angular.

### A13. Bỏ bảng `app_config` khỏi migration đầu tiên

Nếu chốt config đọc từ `appsettings.json` (pending #3, đề xuất đã có sẵn trong [`../docs/decisions.md`](../docs/decisions.md)) thì bảng `app_config` là bảng chết. Đừng tạo bảng mà không có code nào đọc — Phase 2 cần thì thêm migration sau, tốn 1 phút.

---

## Phần A2 — Khối dùng chung (cross-cutting)

Những thứ **mọi feature đều đụng tới**. Làm ở base thì viết một lần, làm sau thì phải sửa lại từng handler. Nguyên tắc chọn: chỉ đưa vào base thứ có **ít nhất 2 chỗ dùng thật trong Phase 1** — còn lại để feature nào cần thì tự viết.

### A14. Paging — `PagedResult<T>` ✅ (code xong ở Batch 2, 15/08/2026)

Dashboard có thể tới ~5.000 video (50 channel × 100 video đang track). Trả hết một lần là hỏng cả API lẫn Angular. Và vì FE là project riêng, **hình dạng response phải chốt ngay từ base** — đổi sau là sửa cả hai đầu.

Dưới đây là **code thật**, không phải sketch — bản đầu của mục này lệch 3 chỗ so với code cuối, xem gạch đầu dòng sau khối.

```csharp
// Application/Common/Models/PagedResult.cs — hình dạng ĐI RA
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    private readonly int _pageSize = PageSize >= 1
        ? PageSize
        : throw new ArgumentOutOfRangeException(nameof(PageSize), PageSize, "PageSize phải ≥ 1.");

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "PageSize phải ≥ 1.");
    }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
}

// Application/Common/Models/PagedQuery.cs — hình dạng ĐI VÀO, base cho mọi query có phân trang
public abstract record PagedQuery
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly int _page = 1;
    private readonly int _pageSize = DefaultPageSize;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize);
    }
}

// Application/Common/Extensions/QueryableExtensions.cs
public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
    this IQueryable<T> query, int page, int pageSize, CancellationToken ct)
{
    var total = await query.CountAsync(ct);
    var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

    return new PagedResult<T>(items, page, pageSize, total);
}
```

**Ba chỗ code thật khác sketch ban đầu** — nếu đọc lại notes rồi gõ theo trí nhớ thì sẽ ra bản cũ:

1. **`PagedQuery.Page` cũng phải chặn**, không chỉ `PageSize`. Sketch để `Page { get; init; } = 1` trần → `?page=0` cho ra `Skip((0-1)*20)` = `Skip(-20)` → Postgres báo *"OFFSET must not be negative"*, tức **500** chứ không phải trang rỗng.
2. **Vượt trần thì kẹp về trần, không reset về default.** Sketch viết `value is < 1 or > MaxPageSize ? 20 : value` — xin 150 lại nhận 20. Bản thật `value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize)`: xin 150 được 100, sát ý định người gọi hơn.
3. **`PagedResult` ném khi `PageSize < 1`.** Bỏ trống thì `TotalCount / (double)0` ra `Infinity`, `(int)Math.Ceiling(Infinity)` trong ngữ cảnh `unchecked` mặc định ra **`int.MinValue`** — `totalPages: -2147483648` đi thẳng ra JSON, không throw không log. Đúng cạm bẫy A4 mục (b).

> ⚠️ Validate phải nằm ở **`init` accessor + backing field**, không phải ở property initializer của positional record. Bản initializer chỉ chạy ở primary constructor — `result with { PageSize = 0 }` đi qua copy-constructor (chép thẳng backing field) nên vẫn lọt.

Hai hành vi ngược nhau cho cùng một field là **cố ý**: `PagedQuery` nhận số từ query string của client → kẹp; `PagedResult` chỉ do code mình dựng → sai là bug, ném. Cùng ranh giới với `Result.Value` (A15) và `VideoStateRules` (S2).

Bốn lưu ý:
- **Cap `PageSize` ngay trong model**, đừng tin FE. Không có auth thì cũng không có ai chặn `?pageSize=999999`.
- ⚠️ `.Skip()` **bắt buộc có `OrderBy` đứng trước, và `OrderBy` phải kèm khóa duy nhất.** Thiếu `OrderBy` thì Postgres trả thứ tự không xác định — bug kinh điển của offset paging. Nhưng **có `OrderBy` vẫn chưa đủ**: sort theo `Score`/`LatestViews` mà 200 video cùng giá trị thì ties vẫn được phép đảo giữa 2 lần query, trang 2 vẫn lặp item. Luôn kết bằng `.ThenBy(x => x.Id)`.
- **Đã cân nhắc nhận `IOrderedQueryable<T>` để compiler ép `OrderBy`, và bỏ.** `Select()` trả `IQueryable` nên type mất tính ordered dù SQL vẫn đúng → chặn nhầm cả code đúng; và nó không chặn được ties, tức bịt cửa dễ để hở cửa khó. Lý do đầy đủ ở [`../docs/decisions.md`](../docs/decisions.md) mục *Batch 2*. Bù lại bằng XML doc `<remarks>` ngay trên `ToPagedResultAsync` — lời nhắc nằm đúng chỗ IntelliSense hiện lúc gõ.
- Offset paging là đủ ở quy mô này. Keyset/cursor chỉ đáng làm khi bảng lên hàng triệu dòng — không phải Phase 1.

### A15. Result pattern — bản đầy đủ ✅ (code xong ở Batch 1, 12/08/2026)

Sketch ban đầu ở [`../docs/architecture.md`](../docs/architecture.md#L172) mới có `Result<T>`; **nay đã cập nhật khớp code thật** ở `Application/Common/Models/Error.cs` + `Models/Result.cs` (dời vào `Models/` ở Batch 2 — xem luật xếp folder cuối S3). Ba điểm bổ sung so với sketch đó:

**(1) `Result` không generic** — cho command không trả gì (`ToggleChannel`, `DeleteSavedIdea`). Không có thì phải viết `Result<bool>` hoặc `Result<Unit>` khắp nơi, xấu và vô nghĩa.

**(2) `Error` chứa được nhiều lỗi field** — FluentValidation trả về một **danh sách** lỗi theo từng field, còn `Error` hiện tại chỉ có 1 `Message`. `ValidationBehavior` sẽ phải nuốt bớt lỗi, và Angular không hiển thị được lỗi dưới từng ô input.

```csharp
public record Error(string Code, ErrorType Type, string Message)
{
    // thêm: lỗi validation nhiều field
    public IReadOnlyDictionary<string, string[]>? Fields { get; init; }

    public static Error Validation(IReadOnlyDictionary<string, string[]> fields) =>
        new("validation.failed", ErrorType.Validation, "Dữ liệu không hợp lệ") { Fields = fields };
}
```

**(3) Implicit conversion — đã cân nhắc rồi BỎ.** Bản đầu định thêm `implicit operator Result<T>(T)` để handler viết `return channel.Id;` thay vì `return Result<int>.Success(channel.Id);`. Bỏ vì mất khả năng đọc/grep: dòng `return channel.Id;` không có chữ nào cho biết đang tạo `Result<int>`, và text-search không tìm ra chỗ construct. Handler luôn gọi tường minh `Result<T>.Success(...)`/`Failure(...)`. Xem [`../docs/decisions.md`](../docs/decisions.md) mục *Application — mục 3, Batch 1*.

### A16. Middleware / pipeline — cần đúng 4 thứ

Xếp theo thứ tự trong `Program.cs`:

| Thứ tự | Thành phần | Ghi chú |
|---|---|---|
| 1 | `UseExceptionHandler` + `IExceptionHandler` | **.NET 8 dùng `IExceptionHandler`**, không viết custom middleware `try/catch` kiểu .NET 6 nữa |
| 2 | `UseSerilogRequestLogging()` | 1 dòng, thay cho việc tự viết logging middleware |
| 3 | `UseCors(policy)` | Phải đứng **trước** `MapControllers` |
| 4 | `MapControllers` | |

**Không** viết middleware cho: auth (single-user), rate limit (một người dùng), transaction (đã bỏ — [`../docs/decisions.md`](../docs/decisions.md)), correlation-id (Serilog `RequestId` có sẵn).

Phân biệt cho rõ: **middleware** xử lý HTTP (exception → JSON, CORS, log request), **MediatR behavior** xử lý nghiệp vụ (validation, log tên command). Job chạy nền **không đi qua middleware** — nên thứ gì cần áp dụng cho cả API lẫn job thì phải đặt ở behavior, không đặt ở middleware.

### A17. Search / filter / sort — làm mức tối thiểu, ❌ đừng làm query engine

Cám dỗ lớn nhất ở đây là dựng một `Specification` pattern hoặc `System.Linq.Dynamic` để "filter gì cũng được". **Đừng.** Filter của Phase 1 là tập **cố định và đã biết trước** ([`../docs/domain/dashboard.md`](../docs/domain/dashboard.md)): channel, score range, views, upload date. Viết thẳng trong handler thì đọc được, EF dịch được, và index nào đang dùng nhìn là biết.

Base chỉ cần đúng 2 thứ:

```csharp
// (1) WhereIf — bỏ được cả rừng if lồng nhau khi filter là optional
public static IQueryable<T> WhereIf<T>(
    this IQueryable<T> q, bool condition, Expression<Func<T, bool>> predicate)
    => condition ? q.Where(predicate) : q;

// (2) Sort theo whitelist — KHÔNG bao giờ ghép string vào query
private static readonly Dictionary<string, Expression<Func<Video, object>>> SortMap = new()
{
    ["score"]       = v => v.TrendingScore!.Score,
    ["views"]       = v => v.LatestViews,
    ["publishedAt"] = v => v.PublishedAt,
};
```

Dùng thành:

```csharp
var query = db.Videos.AsNoTracking()
    .WhereIf(q.ChannelId is not null, v => v.ChannelId == q.ChannelId)
    .WhereIf(q.MinScore is not null,  v => v.TrendingScore!.Score >= q.MinScore)
    .WhereIf(q.FromDate is not null,  v => v.PublishedAt >= q.FromDate);
```

Whitelist sort quan trọng hơn vẻ ngoài của nó: nó chặn FE sort theo cột không có index (query full-scan) và chặn luôn chuyện nhận string tùy ý từ client đưa vào query.

**Full-text search thì sao?** Phase 1 chỉ cần tìm theo title → `EF.Functions.ILike(v.Title, $"%{keyword}%")` là đủ. `tsvector` của Postgres mạnh hơn nhưng kéo theo migration đặc thù Npgsql (A11) và một cột generated — để dành khi thật sự thấy chậm.

### A18. Migration workflow — chốt ai chạy migration

Single-user, nên **auto-migrate lúc startup**, nhưng có cờ tắt:

```csharp
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
    await scope.ServiceProvider.GetRequiredService<YTTrendingDbContext>().Database.MigrateAsync();
```

Dùng `MigrateAsync()`, **tuyệt đối không `EnsureCreated()`** — `EnsureCreated` tạo schema bỏ qua hệ thống migration, sau đó không migrate tiếp được, phải drop DB làm lại.

Quy ước đặt tên migration: `AddSavedIdeaNote`, `AddVideoStatusIndex` — động từ + đối tượng, đọc `Migrations/` là ra lịch sử schema.

### A19. Hợp đồng JSON với Angular — chốt một lần ở base

FE là project riêng nên mỗi lần đổi hình dạng response là sửa hai bên. Chốt sẵn:

- **`camelCase`** — mặc định ASP.NET Core, chỉ cần đừng đụng vào.
- **Enum trả về string**: `AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))`. Không có dòng này thì Angular nhận `status: 1` thay vì `"Tracking"` — vừa khó đọc, vừa vỡ khi thêm giá trị enum ở giữa. Khớp luôn với `HasConversion<string>()` dưới DB (A11).
- **Ngày giờ**: `DateTimeOffset` → ISO 8601 có offset, Angular parse thẳng.
- **Lỗi luôn cùng một hình dạng** — chính là `Error { code, type, message, fields? }` (A15), để Angular viết **một** `HttpInterceptor` xử lý hết.
- **List luôn bọc `PagedResult`** (A14), kể cả khi ít item — đừng chỗ trả mảng trần, chỗ trả object.

### A20. ❌ Những thứ KHÔNG đưa vào base

| Thứ | Vì sao bỏ |
|---|---|
| AutoMapper | Query đã `Select(v => new Dto(...))` trực tiếp — thêm mapper là thêm một lớp phải debug, và mất luôn khả năng EF dịch projection xuống SQL |
| Specification pattern | Cùng lý do bỏ Repository — filter cố định, viết thẳng dễ đọc hơn (A17) |
| Generic `BaseController<T>` / CRUD generic | 3 controller, mỗi cái vài action, chả có gì chung ngoài `ISender` |
| `BaseEntity` gom `Id` + audit (generic hay không) | Khóa **không đồng nhất**, gom lại cũng không dùng được: `video_metric_snapshots.id` là BIGINT còn lại là INT, `trending_scores` lấy luôn `video_id` làm PK nên không có cột `id`. Audit cũng chỉ 2/5 bảng có (A4) → chỉ `Channel` + `Video` vừa, tức là base class cho đúng 2 entity. Giữ `AuditableEntity` đúng tên gọi: tên này chính là bộ lọc của `ChangeTracker.Entries<AuditableEntity>()`, đặt là `BaseEntity` sẽ mời gọi cho cả 5 entity kế thừa rồi audit ngầm chạy lên bảng không có cột |
| Unit of Work | `SaveChangesAsync` đã là UoW |
| Caching (IMemoryCache/Redis) | Chưa biết chỗ nào chậm. Thêm cache trước khi đo là tự tạo bug stale data |
| API versioning | Một client duy nhất do chính mình viết |

### A21. Job phải chịu được lỗi ⚠️ (rà bổ sung)

Ba thứ về `BackgroundService` mà không biết trước thì chắc chắn dính:

- **Exception không bắt trong job giết cả process.** Từ .NET 6, mặc định `BackgroundServiceExceptionBehavior.StopHost` — một lần sync lỗi mạng là **API chết theo**. Phải `try/catch` quanh thân vòng lặp, log rồi chạy tiếp vòng sau.
- **`PeriodicTimer` không tick ngay lần đầu.** `SyncIntervalHours = 6` nghĩa là khởi động xong phải chờ 6 tiếng mới có gì xảy ra. Muốn chạy ngay lúc start thì phải gọi tay một lần trước vòng lặp (và nên có cờ config cho việc đó).
- **Chống chạy chồng.** Có endpoint chạy tay (A5) + timer tự chạy → hai lần sync cùng lúc trên cùng tập video. Một `SemaphoreSlim(1,1)` cấp singleton, ai vào sau thì bỏ qua chứ không xếp hàng.

### A22. Cố định port API + seed data dev (rà bổ sung)

- **Port cố định** trong `launchSettings.json` — `proxy.conf.json` của Angular trỏ vào đó, đổi port là FE gãy.
- **Seed vài channel + video giả** cho môi trường Development, để mở dashboard lên là có dữ liệu mà không cần chờ job hay gọi YouTube. Đi cùng `FakeYouTubeClient` (A6).

### A23. Swagger chỉ để tham chiếu — không auto-gen client ✅ đã chốt

Model bên Angular viết tay, `swagger.json` chỉ dùng để tra cứu. Nghĩa là **không cần** `[ProducesResponseType]` đầy đủ trên mọi action hay đặt `operationId` — bớt được một mớ annotation.

Đổi lại mất đi lưới an toàn: đổi shape DTO ở BE thì **không có gì báo cho FE biết**, chỉ phát hiện lúc chạy. Hai thói quen bù lại, rẻ hơn nhiều so với dựng codegen:
- DTO chỉ đặt ở `Features/**/Dtos`, đổi shape thì grep tên DTO là ra hết chỗ dùng.
- Những gì FE phụ thuộc mạnh nhất — `PagedResult` (A14), hình dạng `Error` (A15), enum trả string (A19) — **chốt cứng ở base và không đổi nữa**. Đổi mấy cái đó là gãy mọi màn hình, không phải một màn.

### A24. README "cách chạy" (rà bổ sung)

5 dòng: tạo DB → set user-secrets → `dotnet ef database update` → `dotnet run` → mở Swagger. Viết lúc vừa làm xong thì còn nhớ; ba tháng sau quay lại thì không.

### A25. ❌ Đã cân nhắc thêm và vẫn bỏ

`/health` endpoint (app chạy local, mở Swagger là biết sống hay chết) · rate limiting · response compression · `AddDbContextPool` · migration bundle · Docker hóa API.

### A26. ⚠️ EF Core convention không tự nhận PK/FK khi entity thiếu navigation property

Phát hiện khi chuẩn bị viết Configuration cho mục 4, trước khi có `YTTrendingDbContext` thật — đúng kiểu lỗi chỉ lộ ra lúc bắt tay code, không lộ ra lúc đọc docs.

**a) `TrendingScore` cần `HasKey` tường minh — đã sửa.** Convention của EF Core chỉ tự nhận property tên `Id` hoặc `{TênClass}Id` làm khóa chính. `TrendingScore.VideoId` không khớp pattern nào (tên class là `TrendingScore`, không phải `Video`) — bỏ qua thì `dotnet ef migrations add` chết ngay với lỗi "no key defined".

```csharp
// Persistence/Configurations/TrendingScoreConfiguration.cs
builder.HasKey(t => t.VideoId);
```

**b) `VideoMetricSnapshot` và `TrendingScore` không có navigation property tới `Video`** (đúng theo quyết định "1 chiều từ Video" ở [`../docs/decisions.md`](../docs/decisions.md)) — nghĩa là quan hệ FK cũng **không** tự động được tạo qua convention, `VideoId` sẽ chỉ là cột `int`/`long` trơn, không có ràng buộc khóa ngoại ở DB. **Đã xử lý ở mục 4** — khai `HasOne<Video>()` không tham số (overload không-navigation) rồi nối `.WithMany()` cho snapshot, `.WithOne(v => v.TrendingScore)` cho score, và `HasForeignKey` tường minh. Khác với `Video.Channel` — cái đó có navigation nên convention tự nhận.

```csharp
// VideoMetricSnapshotConfiguration — 1-nhiều, HasForeignKey không cần tham số kiểu
builder.HasOne<Video>().WithMany().HasForeignKey(s => s.VideoId);

// TrendingScoreConfiguration — 1-1, HasForeignKey<T> BẮT BUỘC có tham số kiểu
// để chỉ rõ bên nào là dependent
builder.HasOne<Video>().WithOne(v => v.TrendingScore).HasForeignKey<TrendingScore>(t => t.VideoId);
```

Đã nghiệm thu bằng `psql \d`: cả `fk_video_metric_snapshots_videos_video_id` lẫn `fk_trending_scores_videos_video_id` đều có thật trong DB. Đây là điểm dễ mất nhất của mục 4 — sai thì migration vẫn chạy ngon, chỉ là cột `int` trơn không ai chặn.

**c) `Infrastructure.csproj` thiếu `ProjectReference` tới `Application` — đã sửa.** Không phải lỗi EF Core mà là lỗi build cơ bản (`YTTrendingDbContext : ..., IYTTrendingDbContext` không compile được nếu thiếu), nhưng cùng nhóm "chỉ lộ ra khi thật sự bắt tay code".

---

## Phần B — Lệnh chạy & tiêu chí nghiệm thu từng bước

> Danh sách việc cần làm nằm ở [`setup-base.md`](setup-base.md). Phần dưới giữ lại **lệnh cụ thể** và **cách nghiệm thu** cho từng bước — mở khi làm tới.

### ~~S0. Chốt pending #3~~ ✅ XONG

Config đọc từ `appsettings.json` + Options pattern. [`../docs/config.md`](../docs/config.md), [`../docs/database.md`](../docs/database.md), [`../docs/decisions.md`](../docs/decisions.md) đã sửa khớp — bảng `app_config` không có trong migration đầu tiên.

Chốt cùng lúc: **FE Angular repo riêng** + **không auto-gen TS client** (A23).

---

### ~~S1. Nền solution~~ ✅ XONG

- [x] Tạo database `yttrending_dev` trên Postgres local (A8)
- [x] `Directory.Packages.props` — pin toàn bộ version (A1)
- [x] `Directory.Build.props` — `WarningsAsErrors=nullable` (A2)
- [x] `.editorconfig`
- [x] `.gitignore`: `logs/` đã nằm trong `[Ll]ogs/` sẵn có
- [x] Xóa 3 file `Class1.cs`

Ghi chú lúc làm:
- Postgres 16 (Homebrew) **chưa từng được `initdb`** — phải `initdb -D /opt/homebrew/var/postgresql@16` rồi `brew services start postgresql@16` trước khi `createdb`. Auth local là `trust`, user `linhtran`, port 5432, socket `/tmp`.
- Tạo luôn `yttrending_test` cho sau này (A8).
- `Directory.Build.props` gom cả `TargetFramework` / `Nullable` / `ImplicitUsings` — 4 csproj đã bỏ `PropertyGroup` riêng.
- `Microsoft.EntityFrameworkCore.Design` khai `PrivateAssets=all` để không rò sang API.
- `GlobalUsings.cs` hiện chỉ khai using của package; using namespace nội bộ (`Domain.Entities`, `Application.Common`…) để comment sẵn, mở khoá khi bước 2/3 tạo ra namespace — khai trước sẽ lỗi CS0246.

**Package theo project** (bản mới nhất trong dòng 8.x, trừ MediatR):

| Project | Package |
|---|---|
| Domain | *(không có)* |
| Application | `MediatR` **12.4.1**, `FluentValidation.DependencyInjectionExtensions`, `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Logging.Abstractions` |
| Infrastructure | `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`, `EFCore.NamingConventions`, `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.Hosting.Abstractions` |
| API | `Swashbuckle.AspNetCore` *(đã có)*, `Serilog.AspNetCore`, `Serilog.Sinks.File` |

✅ Nghiệm thu: `dotnet build` sạch, `psql -d yttrending_dev -c '\conninfo'` connect được.

---

### S2. Domain

- [x] `Enums/VideoStatus.cs` — New / Tracking / Archived
- [x] `Common/AuditableEntity.cs` (A4)
- [x] `Entities/`: `Channel`, `Video`, `VideoMetricSnapshot`, `TrendingScore`, `SavedIdea`

Theo đúng [`../docs/database.md`](../docs/database.md). Quy tắc: **anemic — entity chỉ có property `{ get; set; }`, không method, không ctor, không static factory.** Tạo bằng object initializer.

> ⚠️ Mục này ban đầu làm theo hướng ngược lại (static factory + private setter + invariant trong entity) rồi **đổi sang anemic ngày 07/08/2026**. Số đo lúc quyết định: cả Domain layer chỉ có **2 câu `if`**, 8/15 method là nghi lễ gán thuần quanh `private set`. Lý do đầy đủ + đánh đổi: bảng "Đã cân nhắc và bỏ qua" ở [`../docs/architecture.md`](../docs/architecture.md).

`videos` có thêm 3 cột denormalize `latest_views` / `latest_likes` / `latest_comments` (BIGINT) — Discovery seed lần đầu rồi Metrics Update Job ghi đè mỗi lần sync, dashboard filter/sort theo views không phải join `video_metric_snapshots`. `trending_scores.trending_score` đổi tên cột thành `score` (property Domain là `TrendingScore.Score` — class không được có member trùng tên class, CS0542). Cả hai đã cập nhật ở [`../docs/database.md`](../docs/database.md).

**`required` thay vai trò của factory.** Bỏ `Create(...)` là mất bảo đảm "tạo xong là đủ field" — bù bằng `required` trên cột NOT NULL không có default hợp lý, compiler chặn bằng CS9035. Đổi lại được object initializer có tên từng field, không còn bẫy `Video.Create()` 11 tham số vị trí với `views`/`likes`/`comments` cùng kiểu `long` nằm cạnh nhau.

⚠️ **Hai default bắt buộc khai tường minh** — thứ mà factory từng lo hộ:
- `Channel.IsEnabled { get; set; } = true` — quên là channel vừa add đã bị tắt tracking, job bỏ qua **im lặng**, không có lỗi nào báo.
- `Video.Status { get; set; } = VideoStatus.New` — hiện `New` tình cờ là giá trị 0 của enum nên quên vẫn đúng, nhưng đừng dựa vào thứ tự enum.

⚠️ **Nullable + EF Core** (`WarningsAsErrors=nullable` đang bật — A2): navigation property luôn `= null!` (EF chỉ gán khi `Include`). Không còn ctor nên hết chuyện EF bind tham số.

Hai invariant **không nằm ở Domain** — chúng ở `Application/Common/VideoStateRules.cs`:
- `VideoStateRules.Archive(video, now)` — ném lỗi nếu đã ARCHIVED (terminal state), đồng thời set `ArchivedAt`. Không giới hạn trạng thái nguồn — NEW → ARCHIVED thẳng vẫn hợp lệ (xem [`../docs/domain/video-lifecycle.md`](../docs/domain/video-lifecycle.md))
- `VideoStateRules.StartTracking(video)` — chỉ từ NEW

Đây là **quy ước, không phải ràng buộc compiler**: `video.Status = ...` vẫn compile được. Mọi chỗ đổi trạng thái phải tự đi qua `VideoStateRules` — không có gì nhắc, nên nhớ khi review.

Invariant vi phạm ném `InvalidOperationException`, không tạo `DomainException` riêng — đây là bug gọi sai thứ tự ở Application, không phải lỗi nghiệp vụ dự kiến được nên không đi qua `Result` ([`../docs/architecture.md`](../docs/architecture.md)).

✅ Nghiệm thu: build sạch, không project nào reference vào Domain ngoài Application. Probe `new Video { Title = "x" }` phải ra CS9035 (chứng minh `required` có hiệu lực).

---

### S3. Application — phần Common

- [x] `Common/Models/Result.cs`, `Common/Models/Error.cs` — bản đầy đủ: thêm `Result` không generic + `Error.Fields`. **Không** implicit conversion (A15 đề xuất rồi bỏ — [`../docs/decisions.md`](../docs/decisions.md) mục *Batch 1*)
- [x] `Common/Models/PagedResult.cs`, `Common/Models/PagedQuery.cs` (A14)
- [x] `Common/Extensions/QueryableExtensions.cs` — `ToPagedResultAsync`, `WhereIf` (A14, A17)
- [x] `Common/Interfaces/IYTTrendingDbContext.cs` — 5 `DbSet<T>` + `SaveChangesAsync` *(làm sớm ở mục 4 vì `YTTrendingDbContext` cần implement nó)*
- [ ] `Common/Interfaces/IYouTubeClient.cs` — `GetChannelAsync`, `GetRecentShortsAsync`, `GetVideoStatsAsync` (ký chữ tạm, sửa khi làm Discovery)
- [ ] `Common/Behaviors/`: `LoggingBehavior`, `ValidationBehavior` (gom lỗi vào `Error.Fields`)
- [x] `Common/Options/`: `TrackingOptions`, `TrendingOptions`, `JobOptions` — kèm DataAnnotations (`[Range]`) để `ValidateOnStart` bắt được
- [ ] `DependencyInjection.cs` → `AddApplication()`

**Luật xếp folder trong `Common/`** (chốt ở Batch 2, sau khi chính mục này tự mâu thuẫn — `Common/Result.cs` ở root nhưng `Common/Models/PagedResult.cs` trong folder, dù cả bốn đều là record mang dữ liệu):

- **`Models/`** — mọi type dữ liệu: `Error`, `Result`, `PagedResult`, `PagedQuery`, và DTO dùng chung nếu có.
- **Folder còn lại chia theo vai trò**, không theo "là type gì": `Interfaces/`, `Options/`, `Extensions/`, `Behaviors/`.
- **Root** chỉ giữ thứ không rơi vào hai nhóm trên — hiện là `VideoStateRules.cs` (static class chứa rule, không phải type dữ liệu, cũng không phải vai trò hạ tầng).

Type mới sinh ra sau này cứ theo thứ tự đó mà xếp: là dữ liệu → `Models/`; phục vụ một vai trò hạ tầng → folder vai trò; không cả hai → root.

✅ Nghiệm thu: build sạch. Application **không** reference Npgsql.

---

### S4. Infrastructure — Persistence ✅ ĐÃ LÀM

- [x] `Persistence/YTTrendingDbContext.cs` : `DbContext, IYTTrendingDbContext` + inject `TimeProvider` + override `SaveChanges`/`SaveChangesAsync` cho audit (A4)
- [x] `Persistence/Configurations/` — 5 file `IEntityTypeConfiguration<T>`
  - unique index `youtube_video_id`, `youtube_channel_id`, `saved_ideas.video_id`
  - `HasQueryFilter(v => v.DeletedAt == null)` trên `Video`
  - `Status` → `HasConversion<string>()` (A11)
  - `TrendingScore` cần `HasKey` + FK tường minh, `VideoMetricSnapshot` cần FK tường minh (A26)
  - **`HasMaxLength` / `HasPrecision` phải khai tay theo [`../docs/database.md`](../docs/database.md)** — không khai thì Npgsql map mọi `string` thành `text` (chạy được nhưng lệch schema đã chốt), còn `decimal` không khai `HasPrecision` thì EF cảnh báo *"No store type was specified for the decimal property"*. Cột nào cố tình muốn `text` (`Description`, `Note`) thì bỏ trống **và ghi comment nói rõ là cố ý**, không thì lần sau đọc lại tưởng quên.
  - Không khai `OnDelete`: mặc định của EF cho FK bắt buộc đã là `Cascade`, đúng ý. `required` trên entity tự thành `NOT NULL`, không cần `IsRequired()`.
- [x] `DependencyInjection.cs` → `AddInfrastructure(config)`: `AddDbContext` + `UseNpgsql` + `UseSnakeCaseNamingConvention()` (A3), `AddSingleton(TimeProvider.System)`, `AddScoped<IYTTrendingDbContext>` trỏ về cùng instance — **bind Options còn nợ**, chờ mục 3 có Options class.
- [x] Migration `InitialCreate` + auto-migrate lúc startup có cờ `Database:AutoMigrate` (A18)

```bash
dotnet ef migrations add InitialCreate \
  -p src/YTTrending.Infrastructure -s src/YTTrending.API \
  -o Persistence/Migrations
dotnet ef database update -p src/YTTrending.Infrastructure -s src/YTTrending.API
```

⚠️ **`dotnet-ef` phải cùng dòng version với runtime EF Core.** Tool global 7.0.10 + runtime 8.0.11 → EF từ chối chạy: *"The Entity Framework tools version '7.0.10' is older than that of the runtime '8.0.11'."* Nâng bằng `dotnet tool update --global dotnet-ef --version 8.*` rồi `dotnet ef --version` kiểm lại. Cùng bẫy này lặp lại mỗi lần nâng EF Core.

⚠️ Đi đường Package Manager Console thì cần thêm package `Microsoft.EntityFrameworkCore.Tools` vào Infrastructure mới có cmdlet `Add-Migration`. PMC cũng **tự thêm `Microsoft.EntityFrameworkCore.Design` vào startup project (API)** dạng trần — phải khai lại kèm `PrivateAssets=all` + `IncludeAssets` cho khớp Infrastructure, không thì rò assembly design-time ra output.

⚠️ **`-o Persistence/Migrations` đừng bỏ sót**, và tên migration theo A18 (động từ + đối tượng, PascalCase). Thiếu `-o` là migration rơi vào `Infrastructure/Migrations/`, lệch cấu trúc ở [`../docs/architecture.md`](../docs/architecture.md) — sửa sau thì phải drop DB sinh lại vì `migration_id` nằm trong `__EFMigrationsHistory`.

⚠️ Mở file migration sinh ra **đọc trước khi apply** — tên cột đã snake_case chưa, `status` là `character varying(16)` chưa (không phải enum native), `views/likes/comments` là `bigint` chưa, `trending_scores` PK có đúng là `video_id` và **không** có cột `id` chưa, FK của `video_metric_snapshots` + `trending_scores` có thật chưa (A26).

ℹ️ EF sẽ log `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` — do `Video` có query filter soft-delete còn 3 entity con phụ thuộc bằng FK bắt buộc. Chỉ là nhắc nhở, không vỡ build (`WarningsAsErrors=nullable` chỉ áp cho nullable của C#).

✅ Nghiệm thu: 5 bảng trong Postgres, tên cột đúng như [`../docs/database.md`](../docs/database.md) · `dotnet ef migrations has-pending-model-changes` báo không lệch. **Không** dùng `EnsureCreated()` để tạo schema — nó bỏ qua hệ thống migration, sau đó không migrate tiếp được.

---

### S5. API — wiring

- [ ] `Program.cs`: Serilog (A10) + `AddApplication()` + `AddInfrastructure(builder.Configuration)` + Swagger + `AddProblemDetails()`
- [ ] Pipeline đúng thứ tự: `UseExceptionHandler` → `UseSerilogRequestLogging` → `UseCors` → `MapControllers` (A16)
- [ ] CORS policy cho Angular `http://localhost:4200`, origin đọc từ config, chỉ bật ở Development (A12)
- [ ] `JsonStringEnumConverter` + xác nhận camelCase (A19)
- [ ] `Common/ResultExtensions.cs` — `ToActionResult<T>()` + nhánh `ErrorType.Validation` trả `Error.Fields` cho Angular (A15)
- [ ] `Common/GlobalExceptionHandler.cs` — `IExceptionHandler`
- [ ] `appsettings.json`: section `ConnectionStrings`, `Database`, `Tracking`, `Trending`, `Jobs`, `YouTube`, `Cors`, `Serilog` (giá trị lấy từ [`../docs/config.md`](../docs/config.md))
- [ ] `dotnet user-secrets set` cho connection string + API key (A7)

✅ Nghiệm thu: `dotnet run` → Swagger UI mở được, file log xuất hiện trong `logs/`. Cố tình sửa `SyncIntervalHours: -1` → app **chết lúc startup** (chứng minh `ValidateOnStart` hoạt động).

---

### S6. Vertical slice đầu tiên — `AddChannel`

Slice mỏng nhất chứng minh cả 4 layer thông nhau.

- [ ] `Features/Channels/Commands/AddChannel/` — Command + Handler + Validator
- [ ] `Features/Channels/Queries/GetChannels/` — dùng `PagedQuery` + `ToPagedResultAsync` để nghiệm thu luôn khối paging (A14)
- [ ] `Infrastructure/YouTube/FakeYouTubeClient.cs` (A6)
- [ ] `Controllers/ChannelsController.cs` — `POST` + `GET` (paged)

✅ Nghiệm thu:
- POST qua Swagger → 200 + row trong `channels`, `created_at`/`updated_at` tự điền (chứng minh interceptor chạy — A4)
- POST lại đúng ID đó → **409 Conflict** (Result pattern map status đúng)
- POST rỗng → **400** kèm `fields` chỉ rõ ô nào sai (ValidationBehavior + `Error.Fields` — A15)
- GET `?page=1&pageSize=2` → đúng hình dạng `PagedResult`; `?pageSize=999999` → bị cap về 20 (A14)

---

### ~~S7. Test project~~ — hoãn sang phase sau

Không làm ở base. Khi nào làm, việc còn lại chỉ là: tạo `tests/YTTrending.Application.Tests/`, thêm `xunit` + `FluentAssertions` + `Microsoft.EntityFrameworkCore.Sqlite` + `NSubstitute` + `Microsoft.Extensions.TimeProvider.Testing`, dựng `TestDbContextFactory` (Sqlite `DataSource=:memory:`, giữ connection mở suốt test). Không phải sửa gì trong `src/`.

Đổi lại, phần ✅ nghiệm thu ở S6 và S7 phải làm bằng tay qua Swagger cho đủ — giờ không có test nào đỡ lưng.

---

### S7. Khung background job (chưa có logic)

- [ ] `BackgroundJobs/SyncChannelJob.cs` — `PeriodicTimer` + `CreateScope()`, đọc `JobOptions.Enabled` (A5)
- [ ] `Features/Jobs/SyncChannelsCommand` — handler rỗng, chỉ log
- [ ] `Controllers/JobsController.cs` — `POST /api/jobs/sync` chạy tay

✅ Nghiệm thu: `Jobs:Enabled = false` → không thấy log job. Bật lên → thấy log theo đúng chu kỳ.

Xong S7 là hết base. Feature thật bắt đầu ở [`../docs/domain/discovery-engine.md`](../docs/domain/discovery-engine.md).

---

## Phần C — Quyết định đã chốt

Toàn bộ đã ghi vào [`../docs/decisions.md`](../docs/decisions.md) mục "Setup base", và `config.md` / `database.md` đã sửa cho khớp.

| Quyết định | Ảnh hưởng |
|---|---|
| Config từ `appsettings.json` + Options, bỏ `app_config` khỏi migration đầu | S3, S4, A13 |
| Postgres cài sẵn trên máy, DB `yttrending_dev` — không Docker | S1, S4, A8 |
| FE **Angular repo riêng**, BE là API thuần | S5, A12, A19 |
| `swagger.json` chỉ tham chiếu, **không auto-gen** TS client | A23 |
| `status` = VARCHAR + `HasConversion<string>()` | S4, A11 |
| snake_case qua `EFCore.NamingConventions` | S4, A3 |
| Serilog + file sink, rolling ngày, giữ 7 ngày | S1, S5, A10 |
| Test hoãn sang phase sau | không tạo test project |
| Audit bằng override `SaveChanges`, không dùng interceptor | S4, A4 |

Pending còn lại **không chặn base**: #1 (tách snapshot frequency khỏi sync interval) và #2 (quota YouTube) — chỉ ảnh hưởng khi làm feature thật.
