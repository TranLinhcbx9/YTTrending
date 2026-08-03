# Shorts Discovery Engine

## Mục tiêu

Tìm các Shorts mới từ channel theo dõi, **chỉ giữ lại video đã chứng minh được sức hút** (đạt ngưỡng view tối thiểu).

## Video Tracking Rule

Điều kiện lấy video (Discovery):
- Video đăng trong `RecentDays` ngày gần nhất.
- **OR** nằm trong `RecentShortsLimit` Shorts mới nhất của channel.
- **VÀ phải đạt `MinViewsThreshold`**.

## Flow

```
Channel List
    ↓
Get Latest Shorts (theo RecentShortsLimit)
    ↓
Filter: Views >= MinViewsThreshold?
    ↓ (đạt)                    ↓ (chưa đạt)
Compare Database          Bỏ qua hoàn toàn, không lưu
    ↓                      (sẽ được xem xét lại ở
Detect New Videos          lần sync kế tiếp nếu vẫn
    ↓                      còn trong recent list)
Add Tracking Queue
```

**Nguyên tắc:**
- Không crawl toàn bộ lịch sử channel.
- Chỉ lấy video mới, còn trong phạm vi tracking, **và đã đạt `MinViewsThreshold`**.
- Video chưa đạt ngưỡng: **không lưu bất kỳ record nào** vào hệ thống. Nếu sau này video đó tăng view và vẫn còn nằm trong top recent của channel ở lần sync sau, nó sẽ được bắt lại từ đầu (không có snapshot lịch sử trước thời điểm đạt ngưỡng).

## Duplicate Check (so sánh khi Discovery)

Dùng **VideoId** (ID cố định do YouTube cấp) làm khóa duy nhất để so sánh — không đổi dù title/thumbnail/description bị chỉnh sửa sau. Map trực tiếp vào cột `youtube_video_id` (UNIQUE) trong bảng `videos`.

```
1. Gọi API lấy danh sách Shorts mới nhất của channel (RecentShortsLimit) + filter MinViewsThreshold.
2. Lấy list VideoId vừa fetch được.
3. Query DB: SELECT youtube_video_id FROM videos WHERE youtube_video_id IN (list vừa fetch).
4. So sánh (set difference):
   - VideoId có trong fetch nhưng KHÔNG có trong DB → Video mới → thêm vào Tracking Queue.
   - VideoId có trong cả 2 → Video đã biết → chỉ UPDATE field nếu có thay đổi
     (title, thumbnail...), KHÔNG tạo record mới.
   - VideoId có trong DB nhưng KHÔNG còn trong fetch → rớt khỏi recent list →
     có thể chuyển ARCHIVED (nếu đang TRACKING theo điều kiện RecentShortsLimit).
```

## Liên quan

- Kết quả discovery → [`video-lifecycle.md`](video-lifecycle.md) (trạng thái NEW).
- Chạy định kỳ bởi [`background-jobs.md`](background-jobs.md) (Sync Channel Job).
- Config liên quan: [`../config.md`](../config.md).
