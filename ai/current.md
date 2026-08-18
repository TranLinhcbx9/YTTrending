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
| 6. Slice nghiệm thu (`AddChannel`) | ⬜ |
| 7. Khung background job | ⬜ |

## Đang làm

- **Bước tiếp theo: mục 6** (slice nghiệm thu `AddChannel`) — đầu việc + tiêu chí nghiệm thu ở [`setup-base-notes.md`](setup-base-notes.md) mục S6 (`AddChannelCommand`+validator, `GetChannelsQuery`, `FakeYouTubeClient`, `ChannelsController`, seed Dev).
- **Còn nợ verify tay của mục 5** trước khi coi là đóng hẳn (`dotnet build` đã xanh — 0 warning/0 error, đã tự chạy 19/08/2026):
  - `dotnet run --project src/YTTrending.API` → Swagger mở ở http://localhost:5118, file log xuất hiện trong `logs/`.
  - Cố tình sửa `Tracking:SyncIntervalHours: -1` → app phải **chết lúc startup** (chứng minh `ValidateOnStart` chạy), rồi trả lại giá trị đúng (`6`).

## Lưu ý 🔑 cho bước sau

- **Cho mục 7 (Metrics Update Job):** `VideoStats` cố tình **không** mang mốc thời gian → job phải tự set `SnapshotAt` bằng `TimeProvider`, **một** `now` duy nhất cho cả lượt sync. Chia lô mà mỗi lô một mốc thì snapshot cùng lượt lệch nhau vài chục giây, Velocity (hiệu 2 snapshot) sai theo.
- **Cũng cho mục 7:** `GetVideoStatsAsync` trả list **có thể ngắn hơn input** (video đã xoá/private vắng mặt) — đối chiếu theo `YoutubeVideoId`, tuyệt đối không theo index.

## Nợ verify (chốt ở mục 6)

- **`required` + EF Core 8 materialization** (nợ từ mục 2) — POST qua Swagger → có row → GET đọc lại được.
- **Model binding có ghi được vào `init` accessor không.** Về lý thuyết có (`init` chỉ là setter kèm modreq ở tầng IL, binding đi bằng reflection nên `CanWrite` vẫn `true`) nhưng chưa có đường HTTP nào để chứng minh. Tiêu chí S6 `?pageSize=999999` → cap về 100 là chỗ đóng. Vỡ thì lùi `init` → `set`, mất tính bất biến chứ không mất việc cap.
- **Hình dạng `PagedResult` qua JSON** — `GET ?page=1&pageSize=2` đúng shape đã chốt với FE.
- **Thứ tự trang ổn định** — thêm tiêu chí vào S6: seed ≥4 channel rồi so `?page=1&pageSize=2` với `?page=2&pageSize=2`, **hai trang không được trùng item nào**. Đã bỏ `IOrderedQueryable` nên không có gì chặn lúc compile; đây là chỗ duy nhất bug thứ-tự-bất-định lộ ra trước khi lên Dashboard thật.
- **Chữ ký `IYouTubeClient` có dùng được thật không** (nợ từ Batch 4) — `FakeYouTubeClient` implement được cả 3 method mà không phải sửa interface. Interface thuần thì build luôn xanh, chỉ tới lúc có người implement mới biết thiếu field gì.
- **`ValidationBehavior` trả đúng `Result<T>` fail kèm `fields` camelCase** (nợ từ Batch 5) — reflection dựng `Result<int>.Failure` chỉ đúng/nổ lúc chạy: POST body sai qua Swagger → **400** với `fields` key camelCase (vd `youtubeChannelId`), **không** phải 500. Đóng ở S6.
- **Constraint `where TResponse : IResult` không làm DI bỏ qua behavior** (nợ từ Batch 5) — nếu bị bỏ qua thì validate im lặng không chạy, request lọt xuống handler → **200 thay vì 400**. Cùng đóng ở S6: POST rỗng phải ra 400 chứ không 200.

## Block / Cần quyết định

- Không có gì chặn. Pending #1 (tách snapshot frequency) và #2 (quota YouTube) chỉ ảnh hưởng lúc làm feature thật, không chặn base — xem [`../docs/decisions.md`](../docs/decisions.md).

---

Lịch sử batch/mục đã hoàn thành → [`history.md`](history.md).
