---
name: spec-check
description: Nghiệm thu code đã viết so với plan/docs của dự án — đọc checklist một mục/batch, đối chiếu code trong src/ với notes + docs, báo cáo chỗ lệch. Chỉ đọc, không sửa file, không build, không chạy app. Dùng khi user gõ /spec-check <mục|batch>, hoặc hỏi "code đúng docs chưa", "nghiệm thu mục 3", "check code với plan", "code follow plan chưa".
---

# Nghiệm thu code theo docs

Đối chiếu code đã viết với plan + docs của dự án, chỉ ra chỗ lệch. **Chỉ soi, không sửa.**

## Ràng buộc — đọc trước khi làm bất cứ gì

| ❌ Không được | Lý do |
|---|---|
| `Edit` / `Write` / `NotebookEdit` | Nghiệm thu là chấm, không phải sửa. Tick `- [x]` trong checklist và cập nhật `ai/current.md` **là việc của user** |
| `dotnet build` / `dotnet run` / `dotnet ef` / đụng DB | Đã chốt: skill thuần đọc file. User tự chạy tự test |
| `git` lệnh ghi | — |
| Review naming / perf / style mà docs không nói gì | Đây là nghiệm thu theo docs, không phải code review |

Chỉ dùng `Read`, `Grep`, `Glob`. Báo cáo viết **tiếng Việt**, khớp giọng docs của dự án.

Xong việc thì `git status` phải vẫn sạch.

---

## Bước 1 — Chốt phạm vi

Đọc tham số user truyền vào:

| Tham số | Nghĩa |
|---|---|
| `3` | Mục 3 trong plan file |
| `batch 2` | Batch 2 của mục đang làm — tra `ai/current.md` xem batch đó gồm những gì |
| `S4`, `A14` | Anchor cụ thể trong `ai/setup-base-notes.md` |
| *(trống)* | **Hỏi lại user muốn nghiệm thu mục nào.** Không tự đoán, không tự quét toàn bộ |

Plan file mặc định là `ai/setup-base.md`. User chỉ file khác (vd. một doc trong `docs/domain/`) thì đi theo file đó — cấu trúc bên dưới vẫn áp dụng, chỉ đổi nguồn tiêu chí ở tầng 2/3.

## Bước 2 — Dựng bảng chuẩn TRƯỚC khi mở code

> ⚠️ **Liệt kê xong tiêu chí rồi mới đọc `src/`.** Đọc code trước sẽ sinh ra hợp lý hoá — thấy code viết thế nào thì đọc docs ra thế ấy, và chỗ lệch trở nên "có vẻ hợp lý".

Gom tiêu chí theo **thứ tự thẩm quyền giảm dần**:

1. **`docs/decisions.md`** — cao nhất. Mục "Đã chốt" phủ quyết mọi thứ dưới nó
2. **Plan file** (`ai/setup-base.md`) — mục đó gồm những đầu việc nào, ô nào đã tick
3. **`ai/setup-base-notes.md`** — Phần B, mục `S<n>` tương ứng (lệnh cụ thể + dòng `✅ Nghiệm thu`), cộng **mọi anchor `A<n>` mà các dòng đó trỏ tới**
4. **`docs/*.md`** — spec chi tiết, mở đúng file cần:

| Cần kiểm | Mở |
|---|---|
| Quy tắc phụ thuộc giữa layer, cấu trúc thư mục, trách nhiệm từng project | `docs/architecture.md` |
| Tên bảng/cột, kiểu dữ liệu, index, FK | `docs/database.md` |
| Tên section + giá trị mặc định của config | `docs/config.md` |
| Rule nghiệp vụ (lifecycle, discovery, trending score, dashboard…) | `docs/domain/*.md` |
| Cái gì Phase 1 **không** làm | `docs/out-of-scope.md` |

### Luật xử lý xung đột — phần lõi của skill này

Code mẫu trong `setup-base-notes.md` **có thể đã cũ hơn code thật**. Khi code khác code mẫu:

> **Tra `docs/decisions.md` trước khi kết luận.**
> - **Có ghi ở decisions.md** → code đúng, **không phải finding**. Nếu notes chưa sửa cho khớp thì xếp vào 📄 (docs lệch code)
> - **Không ghi ở đâu** → finding 🟡 *cần quyết*, **không phải** 🔴 — rất có thể là quyết định đúng nhưng chưa kịp ghi

**Tiền lệ có thật, dùng làm chuẩn so sánh:**

- `setup-base-notes.md` A14 viết code mẫu `value is < 1 or > MaxPageSize ? 20 : value`
- Code thật `Common/Models/PagedQuery.cs` viết `value < 1 ? DefaultPageSize : Math.Min(value, MaxPageSize)`
- Chênh này **cố ý**, đã ghi ở `docs/decisions.md` mục *Application — mục 3, Batch 2*: xin 150 nhận 100 sát ý người gọi hơn nhận 20

→ Báo đỏ chỗ này là **sai**. Chính A14 cũng tự ghi "bản đầu của mục này lệch 3 chỗ so với code cuối".

## Bước 3 — Chia tiêu chí: tĩnh vs runtime

Chia **trước khi đối chiếu**, để không biến tiêu chí runtime thành finding giả.

**Kiểm được tĩnh** — làm hết:
- File / type / member có tồn tại không, đặt đúng folder + namespace chưa
- Tên và chữ ký method, kiểu tham số, kiểu trả về
- Chiều phụ thuộc: đọc `.csproj` + `GlobalUsings.cs` + `using` đầu file
- Attribute (`[Range]`, `required`…), giá trị `const`, default của property
- Nội dung file migration so với `docs/database.md`
- XML doc ở chỗ docs nói phải có

**Chỉ chứng minh được lúc chạy** — *không bao giờ là finding*, đẩy thẳng sang cột ⬜:
- "POST qua Swagger → 200", "có row trong DB", "GET đọc lại được"
- "log xuất hiện trong `logs/`", "app chết lúc startup"
- **Cả `dotnet build → 0 warning / 0 error`** — vì skill này không build

Nhớ quét thêm mục **"Nợ verify"** trong `ai/current.md`: món nào thuộc phạm vi đang nghiệm thu thì nhắc lại ở cột ⬜.

## Bước 4 — Đối chiếu

Giờ mới đọc `src/`. Mỗi tiêu chí ghi một trong bốn: **đạt / lệch / thiếu / không kiểm được tĩnh**.

Hai luật giữ báo cáo không trôi thành code review chung chung:

- **Mỗi finding phải trỏ được `file:line` và trích đúng dòng docs làm căn cứ.** Không có căn cứ trong docs → không phải finding
- **Soi cả chiều ngược**: type/file có trong code mà docs không nhắc tới — nó nằm đúng chỗ chưa? (vd. luật xếp folder `Common/` ở cuối `S3`: type dữ liệu vào `Models/`, folder còn lại chia theo vai trò, root chỉ giữ thứ không rơi vào hai nhóm đó)

Việc chưa tick trong checklist mà code chưa có → đó là **"chưa làm"**, ghi ở phần tổng kết, **không phải finding 🔴**.

## Bước 5 — Báo cáo

In ra terminal, theo đúng thứ tự sau. Nhóm nào rỗng thì ghi một dòng "không có" cho gọn, đừng bỏ hẳn.

```
🔴 LỆCH CHỐT
   Code trái một quyết định ở decisions.md, hoặc trái dòng ✅ Nghiệm thu của mục.
   Mỗi mục: file:line · trích dòng docs làm căn cứ · lệch chỗ nào.

🟡 CẦN QUYẾT
   Code khác docs nhưng chưa ghi ở đâu. Nêu CẢ HAI đường và KHÔNG tự chọn:
     (a) sửa code cho khớp docs, hoặc
     (b) giữ code, ghi quyết định vào docs/decisions.md
   Kèm một câu: đường nào nghe hợp lý hơn và vì sao.

📄 DOCS LỆCH CODE
   Code đúng theo quyết định mới nhất, docs/notes chưa sửa theo.
   Đề xuất luôn câu sửa docs (file + đoạn cần thay).

⬜ NỢ VERIFY TAY
   Tiêu chí runtime — chép NGUYÊN VĂN dòng ✅ Nghiệm thu, kèm thao tác user cần làm.

✅ ĐẠT
   1 dòng/tiêu chí. Liệt kê đủ, để user thấy rõ cái gì đã thật sự được soi.
```

Kết bằng **một dòng**:

> `Mục N: đủ điều kiện tick` — khi không còn 🔴 và 🟡
> `Mục N: còn X chỗ phải xử lý` — kèm liệt kê ngắn các đầu việc chưa làm

Rồi nhắc: `ai/setup-base.md` và `ai/current.md` **user tự sửa**, skill không đụng.

### Bug ngoài phạm vi docs

Nếu trong lúc soi thấy bug thật mà docs không nói gì tới — ghi mục riêng **cuối cùng**, tối đa **3 gạch đầu dòng**, tiêu đề rõ ràng *"Ngoài phạm vi nghiệm thu"*. Đừng để phần này nuốt mất báo cáo chính.
