# Metrics Collection & Snapshot

## Metrics Collection

**Lưu thông tin video:**
- VideoId
- ChannelId
- Title
- Description
- Thumbnail
- PublishedAt
- Duration
- Category *(nullable — chưa dùng ở Phase 1, chuẩn bị sẵn cho tương lai)*

**Metrics:**
- Views
- Likes
- Comments

## Metrics Snapshot

Mỗi lần Sync Job chạy → lưu 1 snapshot.

Ví dụ — Video A:

| Thời điểm | Views |
|---|---|
| 08:00 | 10,000 |
| 14:00 | 15,000 |
| 20:00 | 25,000 |

Dùng để tính:
- View Growth
- View Velocity
- Trending Score

## Liên quan

- Snapshot là input cho [`trending-engine.md`](trending-engine.md).
- Snapshot frequency = `SyncIntervalHours` (giá trị ở [`../config.md`](../config.md)); pending "tách riêng khỏi sync interval" ở [`../decisions.md`](../decisions.md).
- Job tạo snapshot: [`background-jobs.md`](background-jobs.md) (Metrics Update Job).
