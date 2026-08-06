# Shorts Trend Monitor — Phase 1

> File index — đọc file này trước, sau đó mở link tương ứng tùy việc đang làm.

## Mục tiêu
Công cụ cá nhân theo dõi kênh YouTube Shorts đối thủ, tự động tổng hợp Shorts gần đây, phát hiện video tăng trưởng tốt để tham khảo ý tưởng content.

→ Chi tiết: [`docs/overview.md`](docs/overview.md)

## Bản đồ tài liệu

| File | Nội dung |
|---|---|
| [`docs/overview.md`](docs/overview.md) | Mục tiêu Phase 1 + Success Criteria |
| [`docs/config.md`](docs/config.md) | Toàn bộ config (Tracking, Trending weights...) |
| [`docs/architecture.md`](docs/architecture.md) | Layer structure, trách nhiệm từng project, background job hosting |
| [`docs/database.md`](docs/database.md) | Schema đề xuất: Channels, Videos, MetricsSnapshots, SavedIdeas |
| [`docs/domain/channel-management.md`](docs/domain/channel-management.md) | Quản lý danh sách channel theo dõi |
| [`docs/domain/discovery-engine.md`](docs/domain/discovery-engine.md) | Rule lấy video + flow discovery + duplicate check |
| [`docs/domain/video-lifecycle.md`](docs/domain/video-lifecycle.md) | Vòng đời video: NEW → TRACKING → ARCHIVED |
| [`docs/domain/metrics-snapshot.md`](docs/domain/metrics-snapshot.md) | Thông tin video + metrics + snapshot |
| [`docs/domain/trending-engine.md`](docs/domain/trending-engine.md) | Công thức tính Trending Score |
| [`docs/domain/dashboard.md`](docs/domain/dashboard.md) | Views chính + filters |
| [`docs/domain/video-detail.md`](docs/domain/video-detail.md) | Chi tiết video + chart |
| [`docs/domain/saved-ideas.md`](docs/domain/saved-ideas.md) | Bookmark video + note (không tag, không bookmark channel) |
| [`docs/domain/background-jobs.md`](docs/domain/background-jobs.md) | Sync Channel Job + Metrics Update Job |
| [`docs/decisions.md`](docs/decisions.md) | Pending items + quyết định đã chốt (ADR-style) |
| [`docs/out-of-scope.md`](docs/out-of-scope.md) | Những gì Phase 1 KHÔNG làm |
| [`ai/current.md`](ai/current.md) | Đang làm module nào, block ở đâu |
| [`ai/setup-base.md`](ai/setup-base.md) | Checklist dựng base BE — chỉ liệt kê việc cần làm |
| [`ai/setup-base-notes.md`](ai/setup-base-notes.md) | Ghi chú chi tiết cho từng mục của checklist: cách làm, code mẫu, cạm bẫy |

## Nguyên tắc chung của dự án

- **Config-first**: toàn bộ thông số (sync interval, tracking window, min views threshold, trending weights, dashboard filters) nằm trong config, không hardcode.
- **VideoId** (do YouTube cấp) là khóa duy nhất để so sánh video, không dùng title/thumbnail vì có thể bị đổi.
- **ARCHIVED là trạng thái cuối** — không có đường quay lại TRACKING.
- Phase 1 là **single-user, không login, không AI, không đa nền tảng** — xem chi tiết [`docs/out-of-scope.md`](docs/out-of-scope.md).
