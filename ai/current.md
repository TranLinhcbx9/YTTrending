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
| 7. Background job thật (Sync + Metrics) | 🔄 Đang làm — xem [`plans/background-job-that.md`](plans/background-job-that.md) |

## Tiến độ feature (ngoài setup base)

| Feature | Trạng thái |
|---|---|
| Channel — full CRUD | ✅ Xong, có seed data |
| Video — Query (List + Detail) | ✅ Xong, seed data Video **chưa làm** |
| Video — Command (Add/Update/Delete) | Không cần đợt này — video do job tạo, xem [`../docs/decisions.md`](../docs/decisions.md) |

## Đang làm

- **Mục 7 resume 01/09/2026 — làm thẳng bản thật (Sync Channel Job + Metrics Update Job), không làm khung rỗng trước.** Lý do hoãn 24/08 (chưa có YouTube API key) hết hiệu lực — key thật đã có. Quyết định + số quota chi tiết ở [`../docs/decisions.md`](../docs/decisions.md) mục *Background job thật*; kế hoạch từng batch ở [`plans/background-job-that.md`](plans/background-job-that.md) (file tạm, xoá sau khi xong).
- **Bước tiếp theo nếu tiếp tục backend**: seed Video giả vào `DevDataSeeder` (gắn vào 4 channel đã seed, đủ 3 status NEW/TRACKING/ARCHIVED) — hiện `GET /api/videos` chạy đúng nhưng DB rỗng vì chưa seed. Chi tiết ở [`history.md`](history.md) mục *Nhật ký — Video feature (Query slice)*.
- **Tạm dừng backend ở đây để bắt đầu FE** (24/08/2026, theo quyết định user) — Channel đã có data thật để dùng ngay; Video có API đúng nhưng cần seed mới có gì để nhìn trên UI.
- **Đã tạo [`docs/api-contract.md`](../docs/api-contract.md)** (25/08/2026) — hợp đồng JSON chi tiết cho FE (endpoint, DTO, error shape thật, pagination), đối chiếu trực tiếp code thay vì suy đoán từ `coding-convention.md` §11 (vốn có vài chỗ sai — đã sửa để trỏ về file mới).
- Mục 6 đóng toàn bộ nợ verify (của chính nó lẫn 2 khoản treo từ mục 5) — 22/08/2026, chi tiết ở [`history.md`](history.md) mục *Nhật ký — mục 6*.

## Lưu ý 🔑 cho bước sau

- **Cho mục 7 (Metrics Update Job):** `VideoStats` cố tình **không** mang mốc thời gian → job phải tự set `SnapshotAt` bằng `TimeProvider`, **một** `now` duy nhất cho cả lượt sync. Chia lô mà mỗi lô một mốc thì snapshot cùng lượt lệch nhau vài chục giây, Velocity (hiệu 2 snapshot) sai theo.
- **Cũng cho mục 7:** `GetVideoStatsAsync` trả list **có thể ngắn hơn input** (video đã xoá/private vắng mặt) — đối chiếu theo `YoutubeVideoId`, tuyệt đối không theo index.

## Block / Cần quyết định

- Không có gì chặn. Pending #1/#2 cũ (snapshot frequency, quota YouTube) đã chốt 01/09/2026 — xem [`../docs/decisions.md`](../docs/decisions.md) mục *Background job thật*.

---

Lịch sử batch/mục đã hoàn thành → [`history.md`](history.md).
