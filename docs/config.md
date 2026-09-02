# Configuration

Toàn bộ thông số hệ thống không hardcode, có thể cấu hình.

## Tracking Configuration

```json
{
  "SyncIntervalHours": 6,
  "MetricsUpdateIntervalHours": 6,
  "RecentDays": 7,
  "RecentShortsLimit": 20,
  "MaxTrackingVideosPerChannel": 100,
  "MinViewsThreshold": 100000,
  "ShortsMaxDurationSeconds": 180,
  "ArchivedRetentionDays": 30
}
```

### Sync

Chu kỳ Sync Channel Job (discovery — phát hiện video mới) và Metrics Update Job (cập nhật view/snapshot/trending cho video đang TRACKING) — chọn riêng 1 trong các mốc cho mỗi job:
- 1h / 3h / 6h / 12h / 24h

`SyncIntervalHours` và `MetricsUpdateIntervalHours` là **2 config độc lập** (tách theo [`decisions.md`](decisions.md) mục *Background job thật*, 01/09/2026) — trước đó gộp chung, snapshot ăn theo chu kỳ sync.

### Video Tracking Rule

`RecentDays`, `RecentShortsLimit`, `MinViewsThreshold` là 3 thông số Discovery dùng để quyết định video nào được tracking — rule đầy đủ (điều kiện OR/AND) ở [`domain/discovery-engine.md`](domain/discovery-engine.md). `ShortsMaxDurationSeconds` là điều kiện riêng, lọc **trước** 3 rule trên: video dài hơn ngưỡng này không phải Shorts, bị loại ngay ở `YouTubeClient` (không phải rule nghiệp vụ tracking).

### Archived Retention

`ArchivedRetentionDays` (mặc định 30 ngày) — video ARCHIVED quá hạn bị Cleanup Job soft-delete (snapshot vẫn giữ). Rule ở [`domain/video-lifecycle.md`](domain/video-lifecycle.md), [`domain/background-jobs.md`](domain/background-jobs.md).

## Trending Engine Config

```json
{
  "ViewGrowthWeight": 60,
  "VelocityWeight": 40
}
```

`MinViewsThreshold` **không** thuộc `TrendingOptions` — dùng chung `TrackingOptions.MinViewsThreshold` ở trên, không lặp lại field (chốt ở decisions.md mục *Batch 3*).

Chi tiết công thức: [`domain/trending-engine.md`](domain/trending-engine.md)

## Jobs & YouTube Client Config

```json
{
  "Jobs": {
    "SyncEnabled": true,
    "MetricsUpdateEnabled": true
  },
  "YouTube": {
    "ApiKey": "",
    "UseFake": true
  }
}
```

- **`Jobs:SyncEnabled` / `Jobs:MetricsUpdateEnabled`** — kill-switch riêng cho từng job (tách từ 1 cờ `Enabled` chung, xem [`decisions.md`](decisions.md) mục *Background job thật*). Ở `appsettings.Development.json` mặc định `false` cả hai — tránh đốt quota YouTube lúc F5 debug. Bật tay qua `POST /api/jobs/sync` / `POST /api/jobs/metrics-update` khi cần test.
- **`YouTube:UseFake`** — `true` dùng `FakeYouTubeClient` (không tốn quota, không cần key), `false` dùng client thật. **`YouTube:ApiKey`** — không để trong `appsettings.json` (giữ placeholder rỗng), set qua `dotnet user-secrets set "YouTube:ApiKey" "<key>"` chạy từ `src/YTTrending.API` (xem [`../ai/setup-base-notes.md`](../ai/setup-base-notes.md) mục A7).

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

- Sync interval và Metrics Update interval (tách riêng).
- Tracking window (RecentDays / RecentShortsLimit).
- Min views threshold để bắt đầu tracking.
- Shorts max duration (lọc video dài thành Shorts).
- Trending score weights (ViewGrowthWeight, VelocityWeight).
- Archived retention (ArchivedRetentionDays).
- Job enable flags + YouTube client thật/giả (Jobs, YouTube).
- Dashboard filters.

### Nguồn config: `appsettings.json` (đã chốt)

Phase 1 đọc config từ **`appsettings.json` + Options pattern**, không dùng bảng `app_config` trong DB:

- Bind qua `services.AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` — sai config thì app **chết lúc khởi động**, không chết lúc job chạy 3h sáng.
- Handler inject `IOptionsMonitor<T>` (không phải `IOptions<T>`) → sửa `appsettings.json` là ăn ngay, không cần restart.
- Giá trị nhạy cảm (connection string, YouTube API key) để trong `dotnet user-secrets`, không commit.

Bảng `app_config` trong [`database.md`](database.md) **chưa dùng ở Phase 1** — chỉ cần thiết khi có màn hình UI cho phép sửa config lúc runtime (Phase 2). Chi tiết: [`architecture.md`](architecture.md), [`decisions.md`](decisions.md).
