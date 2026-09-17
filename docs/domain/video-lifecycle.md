# Video Tracking Lifecycle

```
NEW ──────────┐
 │            ↓
 ↓         ARCHIVED (terminal)
TRACKING ─────┘
```

## NEW

Video vừa phát hiện, đã đạt `MinViewsThreshold` và được lưu như một candidate bền vững. Video giữ NEW trong lượt discovery đã tạo nó.

Ở **lượt sync thành công kế tiếp** của channel, NEW còn trong `RecentDays`, vẫn đạt ngưỡng view đã ghi nhận và còn slot `MaxTrackingVideosPerChannel` sẽ chuyển sang TRACKING. NEW không chiếm tracking quota.

Có thể chuyển thẳng sang ARCHIVED mà **không** qua TRACKING — nếu video ra khỏi `RecentDays` trước khi kịp `VideoStateRules.StartTracking()`. `VideoStateRules.Archive()` chỉ chặn khi đã ARCHIVED (terminal-state), không giới hạn trạng thái nguồn — tránh video kẹt vĩnh viễn ở NEW.

## TRACKING

Đang được theo dõi metrics.

Điều kiện vào TRACKING = candidate NEW từ lượt trước còn trong `RecentDays`, qualify và nằm trong `MaxTrackingVideosPerChannel`; candidate có `PublishedAt` mới hơn được ưu tiên khi quota không đủ. Rule discovery đầy đủ ở [`discovery-engine.md`](discovery-engine.md).

## ARCHIVED

Không còn cần update. Đây là **trạng thái cuối** — video ARCHIVED sẽ **không quay lại TRACKING**, kể cả nếu sau đó có tăng trưởng đột biến.

Ví dụ điều kiện chuyển ARCHIVED:
- Quá thời gian tracking.
- Không còn nằm trong danh sách recent.

Rule terminal-state được enforce ở **Application layer** (`VideoStateRules`), không dùng DB trigger. Đây là **quy ước** (entity anemic nên `video.Status = ...` vẫn compile được) — chi tiết kỹ thuật ở [`../coding-convention.md`](../coding-convention.md) và [`../decisions.md`](../decisions.md).

## Cleanup (Retention)

Video ARCHIVED quá `ArchivedRetentionDays` (mặc định 30 ngày — xem [`../config.md`](../config.md)) sẽ được **soft-delete** bởi Cleanup Job chạy định kỳ. Snapshot lịch sử của video đó **vẫn giữ nguyên**, không xóa theo.

## Liên quan

- Nguồn tạo NEW: [`discovery-engine.md`](discovery-engine.md).
- Video ở trạng thái TRACKING được cập nhật metrics/snapshot bởi Metrics Update Job — xem [`background-jobs.md`](background-jobs.md).
- Cleanup Job dọn ARCHIVED quá hạn — xem [`background-jobs.md`](background-jobs.md).
