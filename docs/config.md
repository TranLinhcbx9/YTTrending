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

Điều kiện lấy video (Discovery):
- Video đăng trong `RecentDays` ngày gần nhất.
- **OR** nằm trong `RecentShortsLimit` Shorts mới nhất của channel.
- **VÀ phải đạt `MinViewsThreshold`** (chi tiết: [`domain/discovery-engine.md`](domain/discovery-engine.md)).

### Archived Retention

Video ở trạng thái ARCHIVED quá `ArchivedRetentionDays` (mặc định 30 ngày) sẽ bị **soft-delete** bởi Cleanup Job — không xóa snapshot lịch sử liên quan. Chi tiết: [`domain/video-lifecycle.md`](domain/video-lifecycle.md), [`domain/background-jobs.md`](domain/background-jobs.md).

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

Toàn bộ các thông số quan trọng đều đưa vào configuration (lưu dạng key-value trong DB, không hardcode trong code):
- Sync interval (= snapshot frequency).
- Tracking window (RecentDays / RecentShortsLimit).
- Min views threshold để bắt đầu tracking.
- Trending score weights (ViewGrowthWeight, VelocityWeight).
- Archived retention (ArchivedRetentionDays).
- Dashboard filters.

Không hardcode trong code để sau khi dùng thực tế có thể tinh chỉnh mà không phải sửa logic hệ thống.
