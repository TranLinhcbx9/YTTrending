# Current Work

> File này ghi lại đang làm module nào, block ở đâu — cập nhật liên tục trong quá trình dev.

## Đang làm

- Setup skeleton: cài package (MediatR 12.x, FluentValidation, EF Core 8 + Npgsql), dựng Domain entities + `AppDbContext` + migration đầu tiên.
- Kiến trúc đã chốt — theo [`../docs/architecture.md`](../docs/architecture.md).

## Block / Cần quyết định

- **Pending #3**: config đọc từ `appsettings.json` hay bảng `app_config`? Cần chốt trước khi viết Options — xem [`../docs/decisions.md`](../docs/decisions.md).
- Các pending khác (#1 snapshot frequency, #2 API quota) không chặn việc dựng skeleton.

## Đã xong

- Solution 4 project (Domain / Application / Infrastructure / API) trên .NET 8, Swagger mặc định.
- Chốt kiến trúc: bỏ Repository, MediatR 12.x, BackgroundService cho job, Result pattern, TimeProvider.
