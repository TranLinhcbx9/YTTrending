---
name: crud-slice
description: Scaffold một CRUD feature slice đầy đủ (Add/Update/Delete/GetById/GetPaged) cho một Domain entity trong codebase YTTrending (.NET 8), dựa trên feature Channel làm mẫu chuẩn (CQRS + MediatR, Repository/UnitOfWork, Result pattern, EF Core 8 + Postgres). Dùng skill này khi user yêu cầu scaffold/thêm CRUD cho một entity, hoặc chỉ cần một phần trong Create/Add, Update/Edit, Delete, GetById, GetPaged/List — ví dụ "scaffold CRUD cho Tag", "thêm CRUD cho SavedIdea", "tạo command Add/Update cho X", "cần list + get by id cho Playlist", hoặc "thêm entity mới end-to-end". Cũng trigger khi user muốn thêm một entity hoàn toàn mới xuyên suốt Domain → Application → Infrastructure → API.
---

# CRUD slice scaffolder (YTTrending)

Skill này sinh ra một CRUD feature slice giống hệt phong cách của feature `Channel` đã có sẵn — cùng cấu trúc folder, cùng naming, cùng convention Result/Repository/pagination. Bản gốc để đối chiếu nằm ở `src/YTTrending.Application/Features/Channels/**`, `src/YTTrending.Infrastructure/Persistence/{Repositories,Configurations}/Channel*.cs`, và `src/YTTrending.API/Controllers/ChannelsController.cs` — chỗ nào bên dưới không rõ thì đọc thẳng các file đó.

## 1. Hỏi/xác nhận trước khi viết code

Đừng đoán các thông tin sau — hỏi user nếu chưa có trong hội thoại:

- **Tên entity** (PascalCase, số ít, vd `Tag`, `SavedIdea`).
- **Field**: tên, kiểu C#, bắt buộc hay optional, field nào là "business key" (id bên ngoài dùng để check trùng — giống `YoutubeChannelId` của `Channel`). Không phải entity nào cũng có business key; nếu không có thì bỏ qua method `ExistsByXAsync` và nhánh conflict trong `AddX`.
- **Cần operation nào**. Chỉ mặc định sinh đủ cả 5 (Add/Update/Delete/GetById/GetPaged) khi user nói "CRUD"/"full CRUD" không kèm điều kiện gì — còn lại sinh đúng cái user yêu cầu (vd "chỉ cần list và get by id" → chỉ 2 query slice, không sinh command, không sinh action ghi trong controller).
- **Entity có cần `created_at`/`updated_at` không?** Theo `docs/coding-convention.md` mục 8, `AuditableEntity` hiện chỉ dùng cho `Channel` và `Video` — không tự động kế thừa. Hỏi user, hoặc suy luận từ ngữ cảnh (vd entity mô tả thứ được sync/theo dõi theo thời gian thì thường cần audit timestamp).
- **Filter cho GetPaged**: có cần field filter nào ngoài `Page`/`PageSize` không (vd `IsEnabled`, enum trạng thái, khoảng ngày)? Nếu có thì thêm vào `<Entity>Filter` và áp bằng `WhereIf` trong repository.

Nếu entity đã tồn tại sẵn ở `src/YTTrending.Domain/Entities/` với field đã định nghĩa, đọc thẳng file đó thay vì hỏi lại — đừng bắt user lặp lại thứ đã có trong code.

## 2. Đảm bảo Domain entity đã tồn tại

`Domain` không phụ thuộc gì cả (không EF, không MediatR, không ASP.NET — xem coding-convention mục 1), nên entity class phải có sẵn ở đó trước, dạng anemic class thuần: chỉ `{ get; set; }`, không constructor/method, field bắt buộc dùng `required`, field có default nghiệp vụ khai tường minh (vd `IsEnabled = true`). Check `src/YTTrending.Domain/Entities/<Entity>.cs`; nếu chưa có thì tạo mới, theo đúng hình dạng của entity mẫu như `Channel.cs`. Chỉ kế thừa `AuditableEntity` nếu bước 1 đã xác nhận cần.

## 3. Sinh slice theo từng layer

Đọc **`references/templates.md`** ngay bây giờ — file đó có template chi tiết từng file (copy nguyên từ feature `Channel` thật) với placeholder `{Entity}`/`{entity}`/`{Entities}`/`{entities}`. Với mỗi operation user yêu cầu, tạo đúng các file trong bảng bên dưới, thay placeholder:

- `{Entity}` → PascalCase số ít (`Tag`)
- `{entity}` → camelCase số ít, chỉ dùng ở chỗ có ghi chú (`tag`)
- `{Entities}` → PascalCase số nhiều (`Tags`) — dùng trong tên query/controller/route
- `{entities}` → chữ thường số nhiều, dùng cho route controller (`api/tags`)

Số nhiều: mặc định thêm `s`; nếu tên entity có số nhiều bất quy tắc (vd `Category` → `Categories`) thì hỏi hoặc dùng đúng dạng hiển nhiên — đừng đoán bừa.

Bảng operation → file (bỏ dòng nào không được yêu cầu):

| Operation | File |
|---|---|
| Add | `Features/<Entity>/Commands/Add<Entity>/{Add<Entity>Command,Add<Entity>CommandHandler,Add<Entity>CommandValidator}.cs` |
| Update | `Features/<Entity>/Commands/Update<Entity>/{Update<Entity>Command,Update<Entity>CommandHandler,Update<Entity>CommandValidator}.cs` |
| Delete | `Features/<Entity>/Commands/Delete<Entity>/{Delete<Entity>Command,Delete<Entity>CommandHandler}.cs` |
| GetById | `Features/<Entity>/Queries/Get<Entity>ById/{Get<Entity>ByIdQuery,Get<Entity>ByIdQueryHandler}.cs` |
| GetPaged | `Features/<Entity>/Queries/Get<Entities>/{Get<Entities>Query,Get<Entities>QueryHandler}.cs`, `Common/Models/<Entity>Filter.cs` |

Luôn sinh các file sau bất kể chọn operation nào (mỗi file được dùng chung bởi ít nhất một operation, và giữ nhất quán cũng rẻ kể cả khi slice chỉ có 1-2 operation):

- `Features/<Entity>/Dtos/<Entity>Dto.cs` (record + extension `<Entity>Mappings.ToDto()`)
- `Features/<Entity>/<Entity>Errors.cs` (chỉ khai error code thật sự dùng tới — `NotFound` nếu có Update/Delete/GetById, `AlreadyExists` chỉ khi có business key và có Add)
- `Common/Interfaces/I<Entity>Repository.cs` + `Infrastructure/Persistence/Repositories/<Entity>Repository.cs` (chỉ có method tương ứng operation được chọn — không sinh `GetPagedAsync` nếu không chọn GetPaged, không sinh `ExistsByXAsync` nếu không có business key)
- `Infrastructure/Persistence/Configurations/<Entity>Configuration.cs` (`HasMaxLength` cho field string, unique index trên business key nếu có)
- `API/Controllers/<Entities>Controller.cs` (chỉ có action tương ứng operation được chọn)

## 4. Giữ đúng các nguyên tắc sau khi viết (không chỉ lúc copy template)

Đây là những chỗ trong `docs/coding-convention.md` dễ bị vi phạm âm thầm nhất khi chuyển template sang entity mới — check lại từng cái sau khi viết xong:

- **Handler luôn trả `Result`/`Result<T>`**, không bao giờ trả kiểu trần — `ValidationBehavior` chỉ ràng buộc trên `IResult`, và DI của MediatR sẽ âm thầm bỏ qua behavior nếu handler không thỏa điều kiện đó, nên trả sai kiểu là mất validate mà không báo lỗi gì. Command không có payload trả về thì dùng `Result`, không dùng `Result<bool>`.
- **Đúng 1 `SaveChangesAsync` mỗi command handler.** Không có transaction behavior nào che cho việc gọi nhiều lần.
- **Query handler dùng `.AsNoTracking()`** và map thẳng vào DTO qua extension `ToDto()` — đừng tạo thêm class mapper riêng.
- **Check trùng dựa trên business key** (unique index riêng + `ExistsByXAsync`), không bao giờ dựa vào field kiểu title/name/thumbnail vì có thể bị sửa.
- **Pagination**: `OrderBy`/`OrderByDescending` trên cột có thứ tự ổn định, luôn kèm `.ThenBy(x => x.Id)` — thiếu thì Postgres không đảm bảo thứ tự ổn định giữa các trang. Filter optional dùng `.WhereIf(condition, predicate)`. `PagedQuery` đã tự kẹp `Page`/`PageSize` — đừng validate lại trong `<Entity>Filter` hay validator mới.
- **Ranh giới lỗi**: lỗi nghiệp vụ dự kiến được (not found, conflict) → `Result.Failure(Error.NotFound(...)/Error.Conflict(...))`. Thứ không nên xảy ra trong luồng dùng bình thường (lỗi hạ tầng, vi phạm invariant) → `throw new InvalidOperationException(...)`, không bọc trong `Result`.
- **`IRepository<T>` giữ nguyên chỉ `GetByIdAsync`/`Create`/`Delete`** — không thêm `Update` generic (EF tự track thay đổi: sửa entity rồi gọi `SaveChangesAsync`), không để lộ `IQueryable`/`Find` qua interface.
- **Entity giữ anemic** — không có logic trên entity, rule/state transition (nếu entity mới có) đặt ở class rule riêng (xem mẫu `VideoStateRules`), không nhét vào phần scaffold này.
- **File-scoped namespace, 1 type/file** (cặp Dto+mapper là ngoại lệ được chấp nhận).
- **Không hardcode tham số nghiệp vụ** — nếu entity cần threshold/limit có thể tune, đưa vào `Options` bind từ `appsettings.json`, không viết cứng trong code scaffold.
- **Không sinh file test.** Phase 1 đang cố tình hoãn test (`AGENTS.md`) — dù có quen sinh test kèm CRUD thì cũng đừng sinh ở đây.

## 5. Sau khi sinh xong, báo cho user 2 việc còn lại phải làm tay

Skill này **cố tình không tự làm** 2 việc sau vì làm mù có thể phá build hoặc phá schema — luôn nhắc rõ khi xong việc:

1. **Đăng ký repository mới trong `src/YTTrending.Infrastructure/DependencyInjection.cs`**: thêm `services.AddScoped<I<Entity>Repository, <Entity>Repository>();` cạnh các dòng `AddScoped` đã có. (Handler MediatR và validator FluentValidation tự động được nhận qua assembly scan — không cần đăng ký tay.)
2. **Sinh và apply EF migration**:
   ```
   dotnet ef migrations add Add<Entity> -p src/YTTrending.Infrastructure -s src/YTTrending.API
   dotnet ef database update
   ```
   (Dev cũng có thể để `Database:AutoMigrate` tự lo thay vì chạy lệnh 2.) Không bao giờ sửa tay file trong `Migrations/` — sai thì sinh lại.

Nếu endpoint mới sẽ được FE Angular dùng, nhắc thêm là `docs/api-contract.md` cần cập nhật theo — compiler không tự phát hiện contract bị lệch ở phía FE.
