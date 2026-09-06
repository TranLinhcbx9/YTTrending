# Shorts Discovery Engine

## Mục tiêu

Tìm các Shorts mới từ channel theo dõi, **chỉ giữ lại video đã chứng minh được sức hút** (đạt ngưỡng view tối thiểu).

## Video Tracking Rule

Điều kiện lấy video (Discovery):
- Chỉ xét Shorts đăng trong `RecentDays` ngày gần nhất.
- Trong tập đó, chỉ giữ tối đa `MaxQualifiedVideosPerChannel` video **mới nhất** đạt `MinViewsThreshold`.
- Nếu không đủ quota thì giữ toàn bộ video đạt ngưỡng trong cửa sổ, không lấy video cũ hơn để bù.

## Flow

```
Channel List
    ↓
Get Shorts pages (đến khi đủ quota hoặc qua RecentDays)
    ↓
Filter: trong RecentDays + Views >= MinViewsThreshold?
    ↓ (đạt)                    ↓ (chưa đạt)
Compare Database          Bỏ qua hoàn toàn, không lưu
    ↓                      (sẽ được xem xét lại ở
Detect New Videos          lần sync kế tiếp nếu vẫn
    ↓                      còn trong recent list)
Persist NEW Candidate
```

**Nguyên tắc:**
- Không crawl toàn bộ lịch sử channel: đọc từng trang uploads mới đến cũ và dừng khi đủ quota hoặc đã qua `RecentDays`.
- Chỉ lấy video còn trong phạm vi tracking, **và đã đạt `MinViewsThreshold`**.
- Video chưa đạt ngưỡng: **không lưu bất kỳ record nào** vào hệ thống. Nếu sau này video đó tăng view và vẫn còn trong `RecentDays`, nó sẽ được bắt lại từ đầu (không có snapshot lịch sử trước thời điểm đạt ngưỡng).

## Duplicate Check (so sánh khi Discovery)

Dùng **VideoId** (ID cố định do YouTube cấp) làm khóa duy nhất để so sánh — không đổi dù title/thumbnail/description bị chỉnh sửa sau. Map trực tiếp vào cột `youtube_video_id` (UNIQUE) trong bảng `videos`.

```
1. Gọi API theo từng trang Shorts mới đến cũ đến khi đủ `MaxQualifiedVideosPerChannel` video qualify hoặc đi qua `RecentDays`.
2. Lấy list VideoId qualify vừa chọn.
3. Query DB: SELECT youtube_video_id FROM videos WHERE youtube_video_id IN (list vừa fetch).
4. So sánh (set difference):
   - VideoId có trong fetch nhưng KHÔNG có trong DB → Video mới → tạo candidate ở trạng thái NEW. Candidate này chỉ được promote sang TRACKING ở lượt sync thành công kế tiếp, theo lifecycle.
   - VideoId có trong cả 2 → Video đã biết → chỉ UPDATE field nếu có thay đổi
     (title, thumbnail...), KHÔNG tạo record mới.
   - VideoId có trong DB nhưng không nằm trong lượt chọn hiện tại vẫn tiếp tục TRACKING; chỉ chuyển ARCHIVED khi `PublishedAt` đã ra khỏi `RecentDays`.
```

## Liên quan

- Kết quả discovery → [`video-lifecycle.md`](video-lifecycle.md) (trạng thái NEW).
- Chạy định kỳ bởi [`background-jobs.md`](background-jobs.md) (Sync Channel Job).
- Config liên quan: [`../config.md`](../config.md).
