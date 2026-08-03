# Architecture

## Mục tiêu

Mô tả kiến trúc kỹ thuật: layer nào chịu trách nhiệm gì, phụ thuộc theo chiều nào. Business rule (discovery, trending, lifecycle...) xem [`domain/`](domain/).

## Layer Structure (Clean Architecture)

```
YTTrending.API            → Composition root: Controllers, DI, appsettings, Swagger
      ↓ depends on
YTTrending.Infrastructure  → EF Core DbContext, Repository impl, YouTube API client, Background Jobs
      ↓ depends on
YTTrending.Application     → Use case (Discovery Engine, Trending Engine, Job orchestration), interface (IVideoRepository, IYouTubeClient...)
      ↓ depends on
YTTrending.Domain          → Entities (Channel, Video, MetricsSnapshot, SavedIdea), Enums — không phụ thuộc project nào khác
```

Nguyên tắc: Domain không biết gì về Infrastructure/Database. Application định nghĩa interface, Infrastructure implement (Dependency Inversion) — đổi DB hoặc đổi cách gọi YouTube API sau này không ảnh hưởng business logic.

## Trách nhiệm từng project

### YTTrending.Domain
- Entities thuần: `Channel`, `Video`, `MetricsSnapshot`, `SavedIdea`.
- Enums: `VideoStatus` (New/Tracking/Archived), `ChannelStatus` (Enabled/Disabled).
- Không chứa logic gọi API, DB, hay tính toán phụ thuộc config.

### YTTrending.Application
- Use case theo từng domain doc:
  - Discovery Engine — [`domain/discovery-engine.md`](domain/discovery-engine.md)
  - Trending Engine — [`domain/trending-engine.md`](domain/trending-engine.md)
  - Job orchestration — [`domain/background-jobs.md`](domain/background-jobs.md)
- Định nghĩa interface cho mọi dependency ra ngoài: `IChannelRepository`, `IVideoRepository`, `IMetricsSnapshotRepository`, `ISavedIdeaRepository`, `IYouTubeClient`.
- Đọc config binding (Tracking/Trending/Dashboard options) — xem [`config.md`](config.md).

### YTTrending.Infrastructure
- Implement các interface khai báo ở Application.
- EF Core `DbContext` + migrations — schema xem [`database.md`](database.md).
- YouTube Data API v3 client.
- Hosting cho Sync Channel Job + Metrics Update Job.

### YTTrending.API
- Composition root: đăng ký DI, đọc appsettings, wiring Options pattern.
- Controllers: Channels, Videos/Dashboard, SavedIdeas.
- Swagger (đã có sẵn trong `Program.cs`).

## Background Job Hosting (chưa chốt)

Chưa chọn cơ chế chạy Sync Channel Job / Metrics Update Job. Phase 1 single-user nên chưa cần dashboard quản lý job phức tạp:
- **Đề xuất:** `BackgroundService` built-in .NET + `PeriodicTimer`, đọc `SyncIntervalHours` từ config — đơn giản, không thêm dependency.
- Cân nhắc Hangfire/Quartz.NET sau nếu cần retry policy hoặc UI theo dõi job.

## External Dependency

- **YouTube Data API v3** — cần API key; quota limit cần kiểm tra trước production (xem [`decisions.md`](decisions.md) pending #2).

## Liên quan

- Database schema: [`database.md`](database.md)
- Toàn bộ domain/business rule: [`../AGENTS.md`](../AGENTS.md) bảng file index
