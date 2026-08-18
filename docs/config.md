# Configuration

Toàn bộ thông số hệ thống không hardcode, có thể cấu hình.

## Tracking Configuration

```json
{
  "SyncIntervalHours": 6,
  "RecentDays": 7,
  "RecentShortsLimit": 20,
  "MaxTrackingVideosPerChannel": 100,
  "MinViewsThreshold": 100000,
  "ArchivedRetentionDays": 30
}
```

### Sync

Chu kỳ đồng bộ dữ liệu (chọn 1 trong các mốc):
- 1h / 3h / 6h / 12h / 24h

> **Lưu ý:** Snapshot metrics được tạo **mỗi lần Sync Job chạy** — nghĩa là `SyncIntervalHours` cũng chính là snapshot frequency. Đây không phải 2 config riêng biệt (hiện tại). Không ảnh hưởng schema — nếu sau này tách riêng thì chỉ cần thêm 1 job riêng, bảng snapshot không đổi.
>
> ⚠️ Xem [`decisions.md`](decisions.md) — pending: có nên tách riêng snapshot frequency khỏi sync interval hay không.

### Video Tracking Rule

`RecentDays`, `RecentShortsLimit`, `MinViewsThreshold` là 3 thông số Discovery dùng — rule đầy đủ (điều kiện OR/AND) ở [`domain/discovery-engine.md`](domain/discovery-engine.md).

### Archived Retention

`ArchivedRetentionDays` (mặc định 30 ngày) — video ARCHIVED quá hạn bị Cleanup Job soft-delete (snapshot vẫn giữ). Rule ở [`domain/video-lifecycle.md`](domain/video-lifecycle.md), [`domain/background-jobs.md`](domain/background-jobs.md).

## Trending Engine Config

```json
{
  "ViewGrowthWeight": 60,
  "VelocityWeight": 40,
  "MinViewsThreshold": 100000
}
```

Chi tiết công thức: [`domain/trending-engine.md`](domain/trending-engine.md)

## Dashboard Filters Config

**Time Range:** 24 Hours / 3 Days / 7 Days / 30 Days

**Filter:**
- Channel
- Score Range
- Views
- Upload Date
- Category *(future)*

Chi tiết: [`domain/dashboard.md`](domain/dashboard.md)

## Config Principle

Toàn bộ các thông số quan trọng đều đưa vào configuration, **không hardcode trong code**, để sau khi dùng thực tế có thể tinh chỉnh mà không phải sửa logic hệ thống:

- Sync interval (= snapshot frequency).
- Tracking window (RecentDays / RecentShortsLimit).
- Min views threshold để bắt đầu tracking.
- Trending score weights (ViewGrowthWeight, VelocityWeight).
- Archived retention (ArchivedRetentionDays).
- Dashboard filters.

### Nguồn config: `appsettings.json` (đã chốt)

Phase 1 đọc config từ **`appsettings.json` + Options pattern**, không dùng bảng `app_config` trong DB:

- Bind qua `services.AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` — sai config thì app **chết lúc khởi động**, không chết lúc job chạy 3h sáng.
- Handler inject `IOptionsMonitor<T>` (không phải `IOptions<T>`) → sửa `appsettings.json` là ăn ngay, không cần restart.
- Giá trị nhạy cảm (connection string, YouTube API key) để trong `dotnet user-secrets`, không commit.

Bảng `app_config` trong [`database.md`](database.md) **chưa dùng ở Phase 1** — chỉ cần thiết khi có màn hình UI cho phép sửa config lúc runtime (Phase 2). Chi tiết: [`architecture.md`](architecture.md), [`decisions.md`](decisions.md).
