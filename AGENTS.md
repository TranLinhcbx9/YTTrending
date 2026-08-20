# Shorts Trend Monitor — Phase 1

> Đọc file này trước. Tài liệu chi tiết chỉ mở khi task cần — đừng đọc hết.

## Mục tiêu
Công cụ cá nhân theo dõi kênh YouTube Shorts đối thủ, tổng hợp Shorts gần đây, phát hiện video tăng trưởng tốt để tham khảo ý tưởng content. → [`docs/overview.md`](docs/overview.md)

## Stack & kiến trúc
.NET 8, 4 tầng: **Domain / Application / Infrastructure / API**. CQRS bằng **MediatR** + Repository/UnitOfWork (đảo lại quyết định ban đầu, giờ là pattern chuẩn — xem [`docs/decisions.md`](docs/decisions.md)) + pipeline behaviors (Logging, Validation). **EF Core 8 + Postgres** (snake_case). **Result pattern** — không ném exception cho luồng nghiệp vụ. Job nền bằng **BackgroundService**, thời gian qua **TimeProvider**. FE **Angular** (repo riêng).
→ Chi tiết tầng/phụ thuộc: [`docs/architecture.md`](docs/architecture.md) · convention code: [`docs/coding-convention.md`](docs/coding-convention.md)

## Invariant (nhớ khi sửa code)
- **Config-first**: mọi thông số (sync interval, tracking window, min views, trending weights, dashboard filters) nằm ở config, không hardcode.
- **VideoId** (YouTube cấp) là khóa duy nhất để so sánh video — không dùng title/thumbnail (có thể bị đổi).
- **ARCHIVED là trạng thái cuối** — không có đường quay lại TRACKING.
- Phase 1 **single-user, không login / không AI / không đa nền tảng** → [`docs/out-of-scope.md`](docs/out-of-scope.md).
- **SSOT — mỗi fact một chủ**: "cái gì" ở [`docs/coding-convention.md`](docs/coding-convention.md), "tại sao / đã bỏ gì" ở [`docs/decisions.md`](docs/decisions.md), **code ở `src/`** (docs không nhúng code), số config ở [`docs/config.md`](docs/config.md), schema ở [`docs/database.md`](docs/database.md). Sửa docs phải giữ nguyên tắc này.

## Lệnh & quy ước
- Build: `dotnet build`
- Chạy API: `dotnet run --project src/YTTrending.API` → Swagger http://localhost:5118
- Migration: `dotnet ef migrations add <Tên> -p src/YTTrending.Infrastructure -s src/YTTrending.API`, apply `dotnet ef database update` (Dev tự migrate qua cờ `Database:AutoMigrate`). ⚠️ `dotnet-ef` phải cùng dòng 8.x với runtime; binary Postgres không nằm trong PATH — chi tiết [`ai/setup-base-notes.md`](ai/setup-base-notes.md).
- Test: Phase 1 CHƯA có test (hoãn) — đừng tự thêm test/CI.
- Commit: conventional commits (`feat:`/`fix:`/`docs:`…); làm trên `develop`, PR về `master`.
- Không sửa tay `src/*/Migrations/` (EF sinh).

## Tài liệu — mở khi task cần (đều nằm trong `docs/`)
- **Nền tảng**: overview · architecture · coding-convention · config · database
- **Domain** (`docs/domain/`): discovery-engine · video-lifecycle · metrics-snapshot · trending-engine · dashboard · video-detail · saved-ideas *(không tag / không bookmark channel)* · channel-management · background-jobs
- **Lý do / ADR**: decisions   ·   **Ngoài phạm vi**: out-of-scope

## Cách làm việc
- Chỉ đọc tài liệu liên quan tới task hiện tại.
- Sửa / append doc to (`decisions.md`, `setup-base-notes.md`…): Grep tìm mục → Read đúng đoạn (offset), **đừng đọc cả file**; sửa xong **không đọc lại để verify** (harness đã theo dõi edit).
- Sửa hành vi domain → đọc file tương ứng trong `docs/domain/` trước.
- Thiết kế chưa rõ → xem [`docs/decisions.md`](docs/decisions.md) trước khi tự nghĩ cách mới.
- Cần biết đang làm gì / block ở đâu → [`ai/current.md`](ai/current.md) (lịch sử đã xong ở [`ai/history.md`](ai/history.md)).
