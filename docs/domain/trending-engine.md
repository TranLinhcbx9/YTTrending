# Trending Engine

## Mục tiêu

Xếp hạng video đáng chú ý dựa trên tốc độ tăng trưởng view (không tính engagement, không tính age).

## Config

```json
{
  "ViewGrowthWeight": 60,
  "VelocityWeight": 40,
  "MinViewsThreshold": 100000
}
```

## Công thức

### Bước 1 — Raw metrics từ 2 snapshot gần nhất

```
ViewGrowth = (ViewsNow - ViewsPrev) / ViewsPrev * 100         // % tăng trưởng
Velocity   = (ViewsNow - ViewsPrev) / HoursBetweenSnapshots   // views/giờ
```

### Bước 2 — Normalize về thang 0–100

So với các video khác đang được tính score tại thời điểm chạy job:

```
Normalize(x) = (x - min(all_x)) / (max(all_x) - min(all_x)) * 100
```

### Bước 3 — Weighted sum

```
TrendingScore = (ViewGrowthNorm * ViewGrowthWeight + VelocityNorm * VelocityWeight) / 100
```

## Lưu ý

- Video mới có **1 snapshot** → chưa tính được Growth/Velocity → tạm thời chưa xếp hạng, chờ đủ 2 snapshot.
- `min/max` để normalize nên tính lại mỗi lần Metrics Update Job chạy (không cache), vì tập video đang track thay đổi liên tục.
- **Lưu trữ:** Trending Score lưu dạng **1 row/video** (ghi đè/UPSERT mỗi lần Metrics Update Job chạy) — không giữ lịch sử theo thời gian. Cần xem biến thiên theo thời gian thì tính lại từ [`metrics-snapshot.md`](metrics-snapshot.md).

## Liên quan

- Input: snapshot từ [`metrics-snapshot.md`](metrics-snapshot.md).
- Output hiển thị ở [`dashboard.md`](dashboard.md) (Trending Shorts / Fast Growing) và [`video-detail.md`](video-detail.md).
- Recalculate mỗi lần Metrics Update Job chạy — [`background-jobs.md`](background-jobs.md).
