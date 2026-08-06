# Current Work

> File này ghi lại đang làm module nào, block ở đâu — cập nhật liên tục trong quá trình dev.

## Đang làm

- Setup base BE theo checklist [`setup-base.md`](setup-base.md) (chi tiết từng mục: [`setup-base-notes.md`](setup-base-notes.md)).
- Quyết định **đã chốt hết**, docs đã sửa khớp. Mục 1 (Nền solution) đã xong. Bước tiếp theo: **mục 2 — Domain** (`VideoStatus`, `AuditableEntity`, 5 entity, invariant `Archive()` / `StartTracking()`).
- Kiến trúc đã chốt — theo [`../docs/architecture.md`](../docs/architecture.md).

## Block / Cần quyết định

- Không có gì chặn. Pending #1 (tách snapshot frequency) và #2 (quota YouTube) chỉ ảnh hưởng lúc làm feature thật, không chặn base — xem [`../docs/decisions.md`](../docs/decisions.md).

## Đã chốt

Toàn bộ quyết định setup base đã ghi vào [`../docs/decisions.md`](../docs/decisions.md) mục "Setup base": config từ `appsettings.json`, Postgres local, FE Angular repo riêng, swagger không auto-gen, status VARCHAR, snake_case, Serilog, test hoãn.

## Đã xong

- Solution 4 project (Domain / Application / Infrastructure / API) trên .NET 8, Swagger mặc định.
- Chốt kiến trúc: bỏ Repository, MediatR 12.x, BackgroundService cho job, Result pattern, TimeProvider.
- **Mục 1 — Nền solution**: `Directory.Packages.props` + `Directory.Build.props` + `.editorconfig` + `GlobalUsings.cs` ×4, package đủ cho 4 layer, `Class1.cs` đã xóa, `dotnet build` sạch 0 warning. Postgres 16 local đã `initdb` + chạy service, có DB `yttrending_dev` và `yttrending_test`.
