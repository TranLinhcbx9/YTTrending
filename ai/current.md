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
| 7. Khung background job | ⬜ |

## Đang làm

- **Bước tiếp theo: mục 7** (Khung background job) — checklist gốc ở [`setup-base.md`](setup-base.md), cách làm ở [`setup-base-notes.md`](setup-base-notes.md) mục S7.
- Mục 6 đóng toàn bộ nợ verify (của chính nó lẫn 2 khoản treo từ mục 5) — 22/08/2026, chi tiết ở [`history.md`](history.md) mục *Nhật ký — mục 6*.

## Lưu ý 🔑 cho bước sau

- **Cho mục 7 (Metrics Update Job):** `VideoStats` cố tình **không** mang mốc thời gian → job phải tự set `SnapshotAt` bằng `TimeProvider`, **một** `now` duy nhất cho cả lượt sync. Chia lô mà mỗi lô một mốc thì snapshot cùng lượt lệch nhau vài chục giây, Velocity (hiệu 2 snapshot) sai theo.
- **Cũng cho mục 7:** `GetVideoStatsAsync` trả list **có thể ngắn hơn input** (video đã xoá/private vắng mặt) — đối chiếu theo `YoutubeVideoId`, tuyệt đối không theo index.

## Block / Cần quyết định

- Không có gì chặn. Pending #1 (tách snapshot frequency) và #2 (quota YouTube) chỉ ảnh hưởng lúc làm feature thật, không chặn base — xem [`../docs/decisions.md`](../docs/decisions.md).

---

Lịch sử batch/mục đã hoàn thành → [`history.md`](history.md).
