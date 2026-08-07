# Video Tracking Lifecycle

```
NEW ──────────┐
 │            ↓
 ↓         ARCHIVED (terminal)
TRACKING ─────┘
```

## NEW

Video vừa phát hiện, đã đạt `MinViewsThreshold`.

Có thể chuyển thẳng sang ARCHIVED mà **không** qua TRACKING — nếu video rớt khỏi recent list (`RecentShortsLimit`) trước khi kịp `StartTracking()`. `Video.Archive()` chỉ chặn khi đã ARCHIVED (terminal-state), không giới hạn trạng thái nguồn — tránh video kẹt vĩnh viễn ở NEW.

## TRACKING

Đang được theo dõi metrics.

Điều kiện:
- `Published Date <= RecentDays`
- **OR** nằm trong Top Recent Shorts (`RecentShortsLimit`)

## ARCHIVED

Không còn cần update. Đây là **trạng thái cuối** — video ARCHIVED sẽ **không quay lại TRACKING**, kể cả nếu sau đó có tăng trưởng đột biến.

Ví dụ điều kiện chuyển ARCHIVED:
- Quá thời gian tracking.
- Không còn nằm trong danh sách recent.

Rule terminal-state này được enforce ở **domain/application layer**, không dùng DB trigger.

## Cleanup (Retention)

Video ARCHIVED quá `ArchivedRetentionDays` (mặc định 30 ngày — xem [`../config.md`](../config.md)) sẽ được **soft-delete** bởi Cleanup Job chạy định kỳ. Snapshot lịch sử của video đó **vẫn giữ nguyên**, không xóa theo.

## Liên quan

- Nguồn tạo NEW: [`discovery-engine.md`](discovery-engine.md).
- Video ở trạng thái TRACKING được cập nhật metrics/snapshot bởi Metrics Update Job — xem [`background-jobs.md`](background-jobs.md).
- Cleanup Job dọn ARCHIVED quá hạn — xem [`background-jobs.md`](background-jobs.md).
