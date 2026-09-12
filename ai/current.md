# Current Work

> Trạng thái đang làm: module nào, bước tiếp, nợ verify, block. Lịch sử batch đã xong → [`history.md`](history.md).

## Tiến độ setup base

Checklist gốc: [`setup-base.md`](setup-base.md) · cách làm từng mục: [`setup-base-notes.md`](setup-base-notes.md)

| Mục | Trạng thái |
|---|---|
| 1. Nền solution | ✅ Xong |
| 2. Domain | ✅ Xong |
| 3. Application — khối dùng chung | ✅ Xong |
| 4. Infrastructure — Persistence | ✅ Xong |
| 5. API — wiring | ✅ Xong |
| 6. Slice nghiệm thu (`AddChannel`) | ✅ Xong |
| 7. Background job thật (Sync + Metrics) | 🔄 Đang làm — Sync-all async ✅; Metrics Update chưa làm |

## Tiến độ feature (ngoài setup base)

| Feature | Trạng thái |
|---|---|
| Channel — full CRUD | ✅ Xong, có seed data |
| Video — Query (List + Detail) | ✅ Xong, seed data Video **chưa làm** |
| Video — Command (Add/Update/Delete) | Không cần đợt này — video do job tạo, xem [`../docs/decisions.md`](../docs/decisions.md) |

## Đang làm

- **Sync-all async — Batch 1–7 đã đóng (12/09/2026).** Migration `20260908170042_AddSyncRuns` đang applied trên `yttrending_dev`; contract/database/background-job SSOT đã đối chiếu với code. Worker xử lý progress durable, recovery không resume và scheduler chỉ tạo `CreateSyncRunCommand(Scheduled)`; manual không bị `Jobs:SyncEnabled` chặn. Nghiệm thu tay worker/API đã hoàn thành trước khi đóng batch; build xác minh độc lập pass với 2 warning có sẵn ở `SyncRunRepository` (CS9107, CS9113), 0 error. Metrics Update không thuộc scope plan này.
- **Plan tạm SyncRun còn giữ lại**: [`plans/sync-run-async.md`](plans/sync-run-async.md) có thay đổi chưa commit từ trước, nên không xóa tự động để tránh mất nội dung. Xóa file này sau khi review/commit phần thay đổi đó.
- **Việc kế tiếp của Mục 7:** triển khai Metrics Update Job độc lập (lấy stats video TRACKING, cùng một `now` cho cả lượt, tạo snapshot rồi tính Trending Score) theo [`../docs/domain/metrics-snapshot.md`](../docs/domain/metrics-snapshot.md), [`../docs/domain/trending-engine.md`](../docs/domain/trending-engine.md) và quyết định *Background job thật*.
- **Việc backend độc lập sau SyncRun**: seed Video giả vào `DevDataSeeder` (gắn vào 4 channel đã seed, đủ 3 status NEW/TRACKING/ARCHIVED) — hiện `GET /api/videos` chạy đúng nhưng DB rỗng vì chưa seed. Chi tiết ở [`history.md`](history.md) mục *Nhật ký — Video feature (Query slice)*.
- **Đã tạo [`docs/api-contract.md`](../docs/api-contract.md)** (25/08/2026) — hợp đồng JSON chi tiết cho FE (endpoint, DTO, error shape thật, pagination), đối chiếu trực tiếp code thay vì suy đoán từ `coding-convention.md` §11 (vốn có vài chỗ sai — đã sửa để trỏ về file mới).
- Mục 6 đóng toàn bộ nợ verify (của chính nó lẫn 2 khoản treo từ mục 5) — 22/08/2026, chi tiết ở [`history.md`](history.md) mục *Nhật ký — mục 6*.

## Lưu ý 🔑 cho bước sau

- **Cho mục 7 (Metrics Update Job):** `VideoStats` cố tình **không** mang mốc thời gian → job phải tự set `SnapshotAt` bằng `TimeProvider`, **một** `now` duy nhất cho cả lượt sync. Chia lô mà mỗi lô một mốc thì snapshot cùng lượt lệch nhau vài chục giây, Velocity (hiệu 2 snapshot) sai theo.
- **Cũng cho mục 7:** `GetVideoStatsAsync` trả list **có thể ngắn hơn input** (video đã xoá/private vắng mặt) — đối chiếu theo `YoutubeVideoId`, tuyệt đối không theo index.

## Block / Cần quyết định

- Không có gì chặn. Pending #1/#2 cũ (snapshot frequency, quota YouTube) đã chốt 01/09/2026 — xem [`../docs/decisions.md`](../docs/decisions.md) mục *Background job thật*.

---

Lịch sử batch/mục đã hoàn thành → [`history.md`](history.md).
