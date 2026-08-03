# Background Jobs

## Sync Channel Job

Chạy theo config: `SyncIntervalHours`

Nhiệm vụ:
- Detect video mới (áp dụng filter `MinViewsThreshold`) — xem [`discovery-engine.md`](discovery-engine.md).
- Update tracking list.

## Metrics Update Job

Nhiệm vụ:
- Update metrics của video đang TRACKING — xem [`video-lifecycle.md`](video-lifecycle.md).
- Create snapshot — xem [`metrics-snapshot.md`](metrics-snapshot.md).
- Recalculate Trending Score — xem [`trending-engine.md`](trending-engine.md).

## Cleanup Job

Nhiệm vụ:
- Quét video ở trạng thái ARCHIVED quá `ArchivedRetentionDays` — xem [`video-lifecycle.md`](video-lifecycle.md).
- Soft-delete (đánh dấu đã xóa, không xóa vật lý) — snapshot liên quan vẫn giữ nguyên.

## Liên quan

- Config chu kỳ chạy + retention: [`../config.md`](../config.md).
- Pending: có nên tách snapshot frequency khỏi sync interval — [`../decisions.md`](../decisions.md).
- Giới hạn API quota (YouTube Data API) cần kiểm tra trước production — [`../decisions.md`](../decisions.md).
