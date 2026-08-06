# Current Work

> File này ghi lại đang làm module nào, block ở đâu — cập nhật liên tục trong quá trình dev.

## Tiến độ setup base

Checklist gốc: [`setup-base.md`](setup-base.md) · cách làm từng mục: [`setup-base-notes.md`](setup-base-notes.md)

| Mục | Trạng thái |
|---|---|
| 1. Nền solution | ✅ Xong |
| 2. Domain | ⬜ **Tiếp theo** |
| 3. Application — khối dùng chung | ⬜ |
| 4. Infrastructure — Persistence | ⬜ |
| 5. API — wiring | ⬜ |
| 6. Slice nghiệm thu (`AddChannel`) | ⬜ |
| 7. Khung background job | ⬜ |

## Đang làm

- Bước tiếp theo: **mục 2 — Domain**: enum `VideoStatus`, `AuditableEntity`, 5 entity (`Channel`, `Video`, `VideoMetricSnapshot`, `TrendingScore`, `SavedIdea`), 2 invariant `Video.Archive()` + `Video.StartTracking()`.
- Làm theo [`../docs/database.md`](../docs/database.md), quy tắc: private setter + static factory `Create(...)`, không object initializer. Chi tiết: [`setup-base-notes.md`](setup-base-notes.md) mục A4 + S2.
- Khi tạo xong namespace `YTTrending.Domain.Entities` / `.Enums` → nhớ mở khoá dòng comment trong `GlobalUsings.cs` của Domain + Application.

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
