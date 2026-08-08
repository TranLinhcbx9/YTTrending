# Current Work

> File này ghi lại đang làm module nào, block ở đâu — cập nhật liên tục trong quá trình dev.

## Tiến độ setup base

Checklist gốc: [`setup-base.md`](setup-base.md) · cách làm từng mục: [`setup-base-notes.md`](setup-base-notes.md)

| Mục | Trạng thái |
|---|---|
| 1. Nền solution | ✅ Xong |
| 2. Domain | ✅ Xong |
| 3. Application — khối dùng chung | ⬜ **Tiếp theo** |
| 4. Infrastructure — Persistence | ⬜ |
| 5. API — wiring | ⬜ |
| 6. Slice nghiệm thu (`AddChannel`) | ⬜ |
| 7. Khung background job | ⬜ |

## Đang làm

- Bước tiếp theo: **mục 3 — Application khối dùng chung**: `Result`/`Error`, `PagedResult`/`PagedQuery`, `QueryableExtensions`, `IAppDbContext`, `IYouTubeClient`, 2 behavior, 3 Options, `AddApplication()`.
- Chi tiết: [`setup-base-notes.md`](setup-base-notes.md) mục S3 + A14/A15/A17.
- Khi tạo xong `YTTrending.Application.Common` → mở khoá dòng comment còn lại trong `GlobalUsings.cs` của Application và Infrastructure.

## Block / Cần quyết định

- Không có gì chặn. Pending #1 (tách snapshot frequency) và #2 (quota YouTube) chỉ ảnh hưởng lúc làm feature thật, không chặn base — xem [`../docs/decisions.md`](../docs/decisions.md).

## Đã chốt

Toàn bộ quyết định setup base đã ghi vào [`../docs/decisions.md`](../docs/decisions.md) mục "Setup base": config từ `appsettings.json`, Postgres local, FE Angular repo riêng, swagger không auto-gen, status VARCHAR, snake_case, Serilog, test hoãn.

## Đã xong

### Trước setup base

- Solution 4 project (Domain / Application / Infrastructure / API) trên .NET 8, Swagger mặc định.
- Chốt kiến trúc: bỏ Repository, MediatR 12.x, BackgroundService cho job, Result pattern, TimeProvider.

### Mục 1 — Nền solution ✅

- [x] **DB local** — Postgres 16 (Homebrew) chưa từng `initdb`, đã khởi tạo cluster `/opt/homebrew/var/postgresql@16` + `brew services start postgresql@16`. Có `yttrending_dev` và `yttrending_test`. Auth `trust`, user `linhtran`, port 5432.
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

⚠️ **Còn nợ verify sang mục 4:** `required` + EF Core 8 materialization. Về lý thuyết EF ghi qua backing field nên không dính check compile-time, nhưng chỉ chứng minh được khi có `AppDbContext` — chạy `dotnet ef migrations add` + query round-trip. Vỡ thì bỏ `required`, không ảnh hưởng quyết định anemic.
