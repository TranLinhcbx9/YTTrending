# Database Schema

## Mục tiêu

Schema đề xuất cho các entity mô tả trong [`domain/`](domain/). Ưu tiên dùng ID do YouTube cấp (VideoId, ChannelId) làm khóa chính thay vì surrogate ID — đúng nguyên tắc "VideoId là khóa duy nhất" ở [`../AGENTS.md`](../AGENTS.md).

# Database Design — Shorts Trend Monitor (Phase 1)

## 1. channels

Danh sách kênh YouTube đang theo dõi.

| Field | Kiểu dữ liệu | Mô tả |
|---|---|---|
| id | INT (PK, identity) | ID nội bộ, số nguyên tự tăng |
| youtube_channel_id | VARCHAR(64) UNIQUE | ID kênh do YouTube cấp, dùng gọi API và chống add trùng |
| name | VARCHAR(255) | Tên kênh hiển thị |
| url | VARCHAR(512) | Link tới kênh gốc |
| is_enabled | BOOLEAN | Bật/tắt việc tracking kênh này |
| last_sync_at | TIMESTAMPTZ, NULL | Thời điểm sync gần nhất |
| created_at | TIMESTAMPTZ | Thời điểm thêm kênh vào hệ thống |
| updated_at | TIMESTAMPTZ | Thời điểm sửa thông tin gần nhất |

---

## 2. videos

Danh sách video Shorts phát hiện được từ các kênh theo dõi.

| Field | Kiểu dữ liệu | Mô tả |
|---|---|---|
| id | INT (PK, identity) | ID nội bộ |
| youtube_video_id | VARCHAR(32) UNIQUE | ID video do YouTube cấp, dùng để so sánh trùng lặp |
| channel_id | INT (FK → channels.id) | Video thuộc kênh nào |
| title | VARCHAR(512) | Tiêu đề video |
| description | TEXT, NULL | Mô tả video |
| thumbnail_url | VARCHAR(512), NULL | Link ảnh thumbnail |
| published_at | TIMESTAMPTZ | Thời điểm video đăng trên YouTube |
| duration_seconds | INT | Thời lượng video (giây) |
| category | VARCHAR(64), NULL | Danh mục nội dung, chưa dùng ở Phase 1 |
| status | ENUM('NEW','TRACKING','ARCHIVED') | Trạng thái vòng đời video |
| deleted_at | TIMESTAMPTZ, NULL | Thời điểm soft-delete, NULL nghĩa là chưa xóa |
| created_at | TIMESTAMPTZ | Thời điểm hệ thống phát hiện video |
| updated_at | TIMESTAMPTZ | Thời điểm cập nhật gần nhất (title/thumbnail đổi...) |

---

## 3. video_metric_snapshots

Lịch sử số liệu (views/likes/comments) của video theo từng lần sync.

| Field | Kiểu dữ liệu | Mô tả |
|---|---|---|
| id | BIGINT (PK, identity) | ID nội bộ, dùng BIGINT vì bảng này tăng rất nhanh |
| video_id | INT (FK → videos.id) | Snapshot của video nào |
| views | BIGINT | Số lượt xem tại thời điểm snapshot |
| likes | BIGINT | Số lượt thích tại thời điểm snapshot |
| comments | BIGINT | Số bình luận tại thời điểm snapshot |
| snapshot_at | TIMESTAMPTZ | Thời điểm lấy số liệu |

---

## 4. trending_scores

Điểm trending hiện tại của video (1 dòng / video, ghi đè mỗi lần tính lại).

| Field | Kiểu dữ liệu | Mô tả |
|---|---|---|
| video_id | INT (PK, FK → videos.id) | Khóa chính đồng thời là khóa ngoại (1-1 với video) |
| view_growth_pct | NUMERIC(10,2) | % tăng trưởng view giữa 2 snapshot gần nhất |
| velocity_per_hour | NUMERIC(14,2) | Tốc độ tăng view (views/giờ) |
| view_growth_norm | NUMERIC(5,2) | ViewGrowth đã chuẩn hóa về thang 0–100 |
| velocity_norm | NUMERIC(5,2) | Velocity đã chuẩn hóa về thang 0–100 |
| trending_score | NUMERIC(5,2) | Điểm trending cuối cùng, dùng để xếp hạng |
| calculated_at | TIMESTAMPTZ | Thời điểm tính điểm gần nhất |

---

## 5. saved_ideas

Video được bookmark lại để tham khảo ý tưởng.

| Field | Kiểu dữ liệu | Mô tả |
|---|---|---|
| id | INT (PK, identity) | ID nội bộ |
| video_id | INT UNIQUE (FK → videos.id) | Video được bookmark, UNIQUE đảm bảo 1 video chỉ bookmark 1 lần |
| note | TEXT, NULL | Ghi chú tự do của người dùng |
| created_at | TIMESTAMPTZ | Thời điểm bookmark |

---

## 6. app_config

Cấu hình hệ thống dạng key-value, không hardcode trong code.

| Field | Kiểu dữ liệu | Mô tả |
|---|---|---|
| key | VARCHAR(64) (PK) | Tên config, ví dụ `SyncIntervalHours` |
| value | VARCHAR(255) | Giá trị config, lưu dạng chuỗi rồi parse theo kiểu ở tầng code |
| updated_at | TIMESTAMPTZ | Thời điểm config bị chỉnh gần nhất |

---

## Ghi chú lựa chọn kiểu dữ liệu

- **INT / BIGINT (GENERATED ALWAYS AS IDENTITY)** thay vì UUID: đơn giản, đủ dùng cho app cá nhân quy mô nhỏ, join nhanh hơn UUID. Dùng cú pháp `IDENTITY` chuẩn SQL thay vì `SERIAL` (SERIAL là cú pháp cũ của Postgres, tạo sequence ngầm khó quản lý quyền hơn).
- **TIMESTAMPTZ** cho mọi mốc thời gian: tránh lỗi lệch múi giờ khi server và client khác timezone.
- **NUMERIC** thay vì FLOAT cho các trường tính toán (score, growth %): tránh sai số dấu phẩy động khi so sánh/sắp xếp.
- **BIGINT** cho views/likes/comments: video viral có thể vượt giới hạn INT (2.1 tỷ).
- **ENUM** cho status: giới hạn giá trị hợp lệ ngay ở DB, tránh nhập sai chuỗi tự do.