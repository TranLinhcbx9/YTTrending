# Background Jobs

## Sync all qua SyncRun — đã triển khai

Sync all không chạy vòng lặp trong HTTP request. `POST /api/jobs/sync` tạo một `SyncRun` với
`triggerType: Manual`, snapshot các channel đang bật thành `SyncRunItem`, enqueue đúng `runId` rồi
trả `202` để FE poll. Cờ `Jobs:SyncEnabled` **không** chặn luồng manual này.

`SyncChannelJob` là scheduler: sau mỗi `Tracking:SyncIntervalHours` nó mới xét tick đầu tiên, không
run ngay lúc app start. Khi `Jobs:SyncEnabled` là `false`, scheduler chỉ log và bỏ tick; khi bật,
nó tạo scope và gửi `CreateSyncRunCommand(Scheduled)`. Command đã có tự chặn run `Pending`/`Running`,
nên scheduler không tự đọc channel, tạo item hay gọi YouTube.

`SyncRunWorker` đọc queue in-memory chỉ chứa `runId`, tạo scope riêng cho mỗi run rồi gửi processor.
Processor chuyển run/item qua các mốc `Running` và terminal, lưu tiến độ sau mỗi mốc; từng item gọi
`SyncChannelCommand(channelId, run.TriggerType)` theo tuần tự. Trigger `Manual` dùng
`ManualSyncCooldownHours`, trigger `Scheduled` dùng `SyncIntervalHours`.

Khi API khởi động, worker mark mọi run `Pending`/`Running` còn từ process trước thành `Interrupted`;
item chưa kết thúc thành `Skipped` với `syncRun.interrupted`. Không có resume vì queue không bền vững
và retry sau crash có thể làm một channel bị xử lý lại.

## Metrics Update Job — chưa triển khai

Nhiệm vụ:
- Update metrics của video đang TRACKING — xem [`video-lifecycle.md`](video-lifecycle.md).
- Create snapshot — xem [`metrics-snapshot.md`](metrics-snapshot.md).
- Recalculate Trending Score — xem [`trending-engine.md`](trending-engine.md).

## Cleanup Job — chưa triển khai

Nhiệm vụ:
- Quét video ở trạng thái ARCHIVED quá `ArchivedRetentionDays` — xem [`video-lifecycle.md`](video-lifecycle.md).
- Soft-delete (đánh dấu đã xóa, không xóa vật lý) — snapshot liên quan vẫn giữ nguyên.

## Liên quan

- Config chu kỳ chạy + retention: [`../config.md`](../config.md).
- Quyết định về lịch chạy, quota và ranh giới job: [`../decisions.md`](../decisions.md).
