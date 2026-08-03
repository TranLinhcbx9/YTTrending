# Decisions & Pending Items

## Đã chốt

- **Snapshot = Sync**: Snapshot metrics được tạo mỗi lần Sync Job chạy. `SyncIntervalHours` chính là snapshot frequency — không phải 2 config riêng biệt (mặc định hiện tại là gộp chung).
- **VideoId là khóa duy nhất**: dùng để so sánh/duplicate-check, không dùng title/thumbnail vì các trường này có thể bị chỉnh sửa.
- **ARCHIVED là trạng thái cuối**: không có đường quay lại TRACKING, kể cả khi video tăng trưởng đột biến sau đó.
- **Video chưa đạt `MinViewsThreshold`**: không lưu bất kỳ record nào; nếu sau đạt ngưỡng và vẫn còn trong recent list thì được bắt lại từ đầu, không có snapshot lịch sử trước đó.
- **Trending Score**: chỉ tính theo View Growth + Velocity, không tính engagement (likes/comments), không tính age của video.
- **Normalize min/max**: tính lại mỗi lần Metrics Update Job chạy, không cache — vì tập video đang track thay đổi liên tục.
- **ARCHIVED terminal-state**: rule "không quay lại TRACKING" được enforce ở domain/application layer, không dùng DB trigger.
- **Trending Score storage**: lưu 1 row/video (UPSERT mỗi lần Metrics Update Job chạy), không giữ lịch sử theo thời gian — cần xem biến thiên thì tính lại từ metrics snapshot.
- **Archived retention**: video ARCHIVED quá `ArchivedRetentionDays` (mặc định 30 ngày) được soft-delete bởi Cleanup Job; snapshot liên quan vẫn giữ nguyên.
- **Saved Ideas**: bỏ Tag, bỏ bookmark Channel — chỉ bookmark Video, 1 video/1 bookmark (khóa duy nhất).

## Pending (chưa chốt)

### 1. Snapshot frequency tách riêng khỏi Sync Interval?

Hiện tại gộp chung: sync channel mỗi `SyncIntervalHours` = tạo snapshot mỗi `SyncIntervalHours`.

Câu hỏi mở: có cần tách riêng — ví dụ sync channel (detect video mới) mỗi 6h, nhưng snapshot metrics mỗi 1h riêng cho video đang hot (đang TRACKING) để tính Trending Score chính xác hơn?

Không ảnh hưởng schema — nếu tách sau này chỉ cần thêm 1 job riêng, bảng snapshot không đổi.

→ Ảnh hưởng: [`config.md`](config.md), [`domain/background-jobs.md`](domain/background-jobs.md), [`domain/metrics-snapshot.md`](domain/metrics-snapshot.md).

### 2. API Quota (YouTube Data API)

Với 20–50 channel, sync mỗi vài giờ, cần kiểm tra thực tế quota YouTube Data API có đủ dùng không trước khi lên production.

→ Ảnh hưởng: [`domain/background-jobs.md`](domain/background-jobs.md), [`domain/channel-management.md`](domain/channel-management.md).
