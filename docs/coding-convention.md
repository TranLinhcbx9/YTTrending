# Coding Convention — Shorts Trend Monitor (.NET 8)

> Bản rút gọn để tra nhanh khi viết code mới / review. **Quy tắc gì** ở đây; **vì sao** xem [`decisions.md`](decisions.md) + [`architecture.md`](architecture.md). Style cơ học do [`.editorconfig`](../.editorconfig) + [`Directory.Build.props`](../Directory.Build.props) ép sẵn — mục 2 chỉ liệt kê, không lặp.

## 1. Layer & chiều phụ thuộc

| Project | Reference | NuGet được phép | Cấm |
|---|---|---|---|
| **Domain** | (không) | (không) | EF Core, MediatR, ASP.NET |
| **Application** | Domain | MediatR 12.x, FluentValidation, `Microsoft.EntityFrameworkCore`, Options, Logging.Abstractions | Npgsql (provider), ASP.NET |
| **Infrastructure** | Application | EF Core + Npgsql, Http.Resilience, Hosting | — |
| **API** | Application + Infrastructure | ASP.NET, Swashbuckle, Serilog | `using` DbContext/entity EF trong Controller |

- Ranh giới thật (compiler không chặn, phải tự giữ): **Controller chỉ biết `ISender` + DTO**, không chạm `YTTrendingDbContext`.
- Interface Application expose ra ngoài: `IYouTubeClient` + Repository pattern (`IRepository<T>` base, repository riêng theo aggregate — vd `IChannelRepository`) + `IUnitOfWork` — pattern chuẩn từ mục 6 (xem [`decisions.md`](decisions.md)).

## 2. Style & format — do `.editorconfig` / `Directory.Build.props` ép

Chỉ nhắc, **không chép lại rule**: file-scoped namespace · `using` ngoài namespace, `System.*` trước · expression-bodied khi 1 dòng · ưu tiên primary constructor · **nullable warning = build error** (`WarningsAsErrors=nullable`) · `EnforceCodeStyleInBuild` · indent 4 space (2 cho `.json`/`.csproj`/`.props`). Version package pin một chỗ ở [`Directory.Packages.props`](../Directory.Packages.props) (Central Package Management) — `.csproj` chỉ `<PackageReference Include>` **không** kèm `Version`.

## 3. Naming convention

| Thành phần | Quy tắc | Ví dụ |
|---|---|---|
| Namespace / class / record / struct / enum | PascalCase | `TrackingOptions`, `ChannelInfo`, `VideoStatus` |
| Interface | `I` + PascalCase | `IYouTubeClient`, `IResult` |
| Method (kể cả async) | PascalCase | `StartTracking`, `GetChannelAsync` |
| Property / public field | PascalCase | `YoutubeVideoId`, `LatestViews` |
| Private field | `_camelCase` | `_value`, `_pageSize` |
| Local / tham số | camelCase | `request`, `services`, `failures` |
| Hằng (`const`) | PascalCase | `SectionName`, `MaxPageSize` |
| Enum member | PascalCase | `New`, `Tracking`, `Archived` |
| Generic type param | `T` hoặc `T` + tên | `T`, `TRequest`, `TResponse` |

- **Async** → hậu tố `Async`; **`CancellationToken`** → luôn đặt tên `ct`, là tham số cuối.
- **Boolean** → tiền tố `Is`/`Has`/`Can`: `IsSuccess`, `IsEnabled`, `HasNext`.
- **Acronym/tên riêng** viết như một từ thường: `Id`, `Url`, `Youtube` (không `ID`/`URL`/`YouTube`). Ngoại lệ giữ nguyên: prefix dự án `YT` (`YTTrending`, `YTTrendingDbContext`) và brand `IYouTubeClient`.
- **Hậu tố theo vai trò**: `...Command`/`...Query` (request), `...CommandHandler`/`...QueryHandler`, `...CommandValidator`, `...Dto` (DTO ra FE), `...Options` (config), `...Behavior` (pipeline), `...Configuration` (EF mapping).
- **Tên file = tên type** (`Video.cs`); gom nhiều type cùng một hợp đồng vào 1 file khi hợp lý (`YouTubeModels.cs`, `Result.cs` chứa `IResult`+`Result`+`Result<T>`).

## 4. Cấu trúc feature & thư mục

- **1 feature = 1 folder** `Features/<Domain>/Commands|Queries/<Name>/` — Command/Query + Handler + Validator cạnh nhau; sửa 1 tính năng mở đúng 1 folder.
- **Luật xếp `Common/`**: type dữ liệu → `Models/`; còn lại theo vai trò (`Interfaces`/`Options`/`Extensions`/`Behaviors`); root chỉ giữ thứ không thuộc 2 nhóm (hiện là `VideoStateRules`).
- Persistence: EF config ở `Persistence/Configurations/`, migration ở `Persistence/Migrations/`.

## 5. Handler (CQRS qua MediatR 12.x)

- Handler **LUÔN** trả `Result` / `Result<T>` — **ràng buộc thật**: `ValidationBehavior` ràng `where TResponse : IResult`; DI .NET 7+ **lặng lẽ bỏ qua** behavior không thỏa → trả kiểu khác thì validate không chạy mà không báo.
- **Command (ghi)**: qua Repository tương ứng + `IUnitOfWork.SaveChangesAsync()` — pattern chuẩn từ mục 6, xem [`decisions.md`](decisions.md); đổi trạng thái video qua `VideoStateRules`; **1 `SaveChangesAsync`/handler** (không `TransactionBehavior`).
- **Query (đọc)**: qua Repository tương ứng; implementation `.AsNoTracking()`; map thẳng vào DTO, không load entity thừa.
- Duplicate-check theo `YoutubeVideoId`/`YoutubeChannelId` (unique index), **không** theo title/thumbnail.

## 6. Result & Error — ranh giới lỗi

- Lỗi **nghiệp vụ dự kiến được** (duplicate, not found) → trả `Result`/`Result<T>`. Lỗi **hạ tầng/bug** (mất DB, gọi sai thứ tự) → **throw** `InvalidOperationException`, không bọc `Result`.
- Tạo `Result` **tường minh**: `Result<int>.Success(id)` / `.Failure(error)` — không implicit conversion.
- `Result<T>.Value` khi đã fail → **ném** `InvalidOperationException`, không trả `default`.
- Command không có giá trị trả → `Result` (không `Result<bool>` vô nghĩa).
- `Error` mang `ErrorType` (Validation/NotFound/Conflict) → API map HTTP **một chỗ** (`ResultExtensions.ToActionResult`).
- Invariant sai (`VideoStateRules`) → `InvalidOperationException`, **không** tạo `DomainException` riêng.

## 7. Query đọc & phân trang

- Phân trang qua `ToPagedResultAsync`; **bắt buộc** `OrderBy` theo cột có thứ tự ổn định và **luôn** `.ThenBy(x => x.Id)` (thiếu → Postgres không cam kết thứ tự → trang 2 lặp/mất item).
- Filter optional: dùng `WhereIf(condition, predicate)` thay vì if lồng nhau.
- `PagedQuery` **kẹp** `Page`/`PageSize` từ client (không tin input); `DefaultPageSize`/`MaxPageSize` là `private const` — ngoại lệ có chủ ý với config-first (hằng bảo vệ API, không phải thông số nghiệp vụ cần tune).

## 8. Entity & EF mapping

- Entity **anemic**: chỉ `{ get; set; }`, không method/ctor/factory. Field bắt buộc dùng `required` (compiler chặn CS9035). Field có default nghiệp vụ khai tường minh (`IsEnabled = true`, `Status = VideoStatus.New`).
- `AuditableEntity` (`created_at`/`updated_at` tự điền) **chỉ** cho `Channel` + `Video`; thời gian nghiệp vụ (`snapshot_at`/`archived_at`/`calculated_at`) set tay tại chỗ tạo record.
- Navigation **1 chiều từ `Video`** (không collection ngược, tránh `Include` cả nghìn dòng snapshot).
- Mapping bằng **Fluent API** (`IEntityTypeConfiguration<T>`), **không** rải Data Annotation lên entity. `HasMaxLength`/`HasPrecision` khai tay (không thì `string`→`text`). `status`: VARCHAR + `HasConversion<string>()` (không native enum). `HasQueryFilter` soft-delete (`DeletedAt == null`; muốn xem bản đã xóa → `IgnoreQueryFilters()`). **FK khai tay** cho entity không có navigation 2 chiều (`VideoMetricSnapshot`, `TrendingScore`).
- Tên bảng/cột `snake_case` (EFCore.NamingConventions). Migration: động từ + đối tượng PascalCase (`InitialCreate`). Áp DB bằng `MigrateAsync()`, **tuyệt đối không** `EnsureCreated()`.

## 9. Config & thời gian

- **Config-first**: mọi thông số nghiệp vụ (interval, threshold, weights, retention) → Options + `appsettings.json`, **không hardcode**. Đăng ký `AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` (sai config → chết lúc start, không chết lúc job chạy).
- Handler inject `IOptionsMonitor<T>` (đọc nóng khi sửa `appsettings.json`), **không** `IOptions<T>`.
- Mỗi thông số **một nguồn duy nhất** (vd `MinViewsThreshold` chỉ ở `TrackingOptions`).
- **Thời gian**: inject `TimeProvider`, dùng `GetUtcNow()` — **không** `DateTime.UtcNow`. Đo *khoảng* thời gian (elapsed) dùng `Stopwatch.GetTimestamp()`. Một `now` duy nhất cho cả lượt sync.

## 10. Background job

- `BackgroundService` + `PeriodicTimer` (không Hangfire/Quartz).
- **BẮT BUỘC `CreateScope()` mỗi tick** — job là singleton, `ISender`/`DbContext` là scoped; inject thẳng qua ctor sẽ chết lúc startup hoặc giữ một `DbContext` sống mãi.
- Job chỉ là **cái đồng hồ**; logic nằm trong Command (`Features/Jobs/`) để test + gọi tay được. Job **tự** try/catch (không có middleware bắt hộ).
- Đăng ký behavior: `Logging` **trước** `Validation` (Logging bọc ngoài mới log được cả nhánh validation fail).

## 11. Hợp đồng JSON với FE (Angular repo riêng)

- `camelCase` · enum trả **string** · list bọc `PagedResult` · lỗi cùng một hình dạng `Error { code, type, message, fields }` · `Error.Fields` key **camelCase**.
- Đổi shape DTO là **phải tự sửa** interface bên FE (compiler không báo giúp). API bật CORS.

## 12. Test & commit

- Test (Phase 1 hoãn) — khi viết: EF Core **Sqlite in-memory** (không EF InMemory provider), mock `IYouTubeClient`, `FakeTimeProvider` để tua thời gian.
- Commit: Conventional Commits — `feat|fix|chore|docs(scope): mô tả` (theo lịch sử repo).
