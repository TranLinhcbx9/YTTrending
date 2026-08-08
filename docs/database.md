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
| status | VARCHAR(16) | Trạng thái vòng đời video: `New` / `Tracking` / `Archived`. Lưu chuỗi qua `HasConversion<string>()`, **không** dùng native Postgres ENUM — xem ghi chú cuối file |
| latest_views | BIGINT | View tại lần snapshot gần nhất — denormalize từ `video_metric_snapshots` để dashboard filter/sort theo views không phải join. **Discovery seed lần đầu** (bước lọc `MinViewsThreshold` đã cầm sẵn số liệu — xem ghi chú cuối file), sau đó Metrics Update Job ghi đè mỗi lần sync |
| latest_likes | BIGINT | Like tại lần snapshot gần nhất, cùng cơ chế với `latest_views` |
| latest_comments | BIGINT | Comment tại lần snapshot gần nhất, cùng cơ chế với `latest_views` |
| archived_at | TIMESTAMPTZ, NULL | Thời điểm chuyển sang ARCHIVED. Đồng hồ đếm `ArchivedRetentionDays` của Cleanup Job — xem ghi chú cuối file |
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
| score | NUMERIC(5,2) | Điểm trending cuối cùng, dùng để xếp hạng. Tên cột (không phải `trending_score`) vì property tương ứng ở Domain là `TrendingScore.Score` — class không được có member trùng tên class (CS0542) |
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
| updated_at | TIMESTAMPTZ | Thời điểm sửa `note` gần nhất |

---

## 6. app_config — ⚠️ CHƯA DÙNG Ở PHASE 1

> Phase 1 đọc config từ `appsettings.json` + Options pattern ([`config.md`](config.md)), **không tạo bảng này trong migration đầu tiên**. Giữ lại mô tả ở đây cho Phase 2, khi có UI sửa config lúc runtime.

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
- **VARCHAR cho status, không dùng native Postgres ENUM**: native ENUM chặn giá trị sai chặt hơn, nhưng mỗi lần thêm một trạng thái phải viết migration `ALTER TYPE` thủ công (EF Core không tự sinh), và làm vỡ bước tạo schema khi test bằng Sqlite in-memory. Giá trị hợp lệ đã được `VideoStatus` enum ở tầng Domain chặn rồi.
- **Tên bảng/cột dùng `snake_case`**: EF Core mặc định sinh `PascalCase`, nên phải bật `UseSnakeCaseNamingConvention()` (package `EFCore.NamingConventions`) — quyết định này phải có **trước migration đầu tiên**.
- **`created_at` / `updated_at` của `channels` và `videos`** điền tự động: `AppDbContext` override `SaveChanges`/`SaveChangesAsync`, duyệt `ChangeTracker` và lấy giờ từ `TimeProvider` — không set tay ở handler. Lưu ý `ExecuteUpdateAsync` không đi qua `SaveChanges` nên phải tự set `updated_at` trong câu update đó.
- **`archived_at` là cột bắt buộc, không thể thay bằng `updated_at`**: rule retention ở [`domain/video-lifecycle.md`](domain/video-lifecycle.md) tính từ lúc video **chuyển sang ARCHIVED**, trong khi `updated_at` bị đẩy lại mỗi lần Sync Job sửa title/thumbnail. Dùng `updated_at` làm đồng hồ retention thì video ARCHIVED nào bị đổi tiêu đề sẽ không bao giờ đủ hạn để Cleanup Job dọn. Cột này do `VideoStateRules.Archive()` set, thuộc nhóm thời gian nghiệp vụ (set tường minh), không phải audit.
- **`latest_views/likes/comments` được seed ngay lúc Discovery, không chờ Metrics Update Job**: Discovery chỉ nhận video đã đạt `MinViewsThreshold` ([`domain/discovery-engine.md`](domain/discovery-engine.md)) nên tại thời điểm tạo record nó **đã cầm sẵn** views/likes/comments từ API. Để trống chờ job kế tiếp thì video vừa được nhận *vì* có 100k view lại hiện 0 view trên dashboard. Vẫn chỉ có hai đường ghi và đều ghi đè toàn bộ, không cộng dồn — không có nguy cơ lệch số.
- **`snapshot_at` và `calculated_at` KHÔNG phải audit field** — chúng là dữ liệu nghiệp vụ (thời điểm đo số liệu / thời điểm tính điểm), phải set tường minh ở chỗ tạo record, không để interceptor điền ngầm.
- **`DateTimeOffset` cho mọi cột thời gian ở tầng code** (map sang `TIMESTAMPTZ`), không dùng `DateTime` — khớp với `TimeProvider.GetUtcNow()` và không phải đoán `DateTimeKind`.