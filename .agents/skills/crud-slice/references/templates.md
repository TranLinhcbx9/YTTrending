# Template chi tiết — copy nguyên khối, thay placeholder

Placeholder dùng trong toàn bộ file này:
- `{Entity}` — PascalCase số ít, vd `Tag`
- `{entity}` — camelCase số ít (chỉ dùng khi có ghi chú riêng)
- `{Entities}` — PascalCase số nhiều, vd `Tags`
- `{entities}` — chữ thường số nhiều, dùng cho route, vd `tags`

Namespace gốc: `YTTrending.Application`, `YTTrending.Infrastructure`, `YTTrending.API`, `YTTrending.Domain`. `global using` đã cover `FluentValidation`, `MediatR`, `Microsoft.EntityFrameworkCore`, `YTTrending.Domain.Entities`, `YTTrending.Domain.Enums`, `YTTrending.Application.Common`, `YTTrending.Application.Common.Extensions`, `YTTrending.Application.Common.Models` trong project Application — nên hầu hết file dưới đây **không cần** using cho mấy namespace đó, chỉ cần using riêng cho Dto/Interfaces của chính feature.

---

## Domain entity (chỉ tạo nếu chưa có)

`src/YTTrending.Domain/Entities/{Entity}.cs`

```csharp
namespace YTTrending.Domain.Entities;

public class {Entity} // : AuditableEntity — chỉ thêm nếu đã xác nhận cần created_at/updated_at
{
    public int Id { get; set; }
    // field bắt buộc:
    public required string SomeField { get; set; }
    // field có default nghiệp vụ khai tường minh, vd:
    public bool IsEnabled { get; set; } = true;
}
```

Field thật thay theo thông tin user cung cấp ở bước 1 của SKILL.md. Nếu có business key (id ngoài dùng check trùng), đặt tên rõ ràng kiểu `Youtube{Entity}Id`/`External{Entity}Id` tuỳ ngữ cảnh.

---

## Dto + mapper

`src/YTTrending.Application/Features/{Entity}/Dtos/{Entity}Dto.cs`

```csharp
namespace YTTrending.Application.Features.{Entity}.Dtos;

public record {Entity}Dto(
    int Id,
    string SomeField,
    bool IsEnabled);
    // liệt kê đủ field cần trả cho FE — camelCase khi serialize JSON tự động qua System.Text.Json, giữ PascalCase ở C#

public static class {Entity}Mappings
{
    public static {Entity}Dto ToDto(this {Entity} e) =>
        new(e.Id, e.SomeField, e.IsEnabled);
}
```

---

## Errors

`src/YTTrending.Application/Features/{Entity}/{Entity}Errors.cs`

```csharp
namespace YTTrending.Application.Features.{Entity};

public static class {Entity}Errors
{
    public const string NotFound = "{entity}.notFound";       // cần nếu có Update/Delete/GetById
    public const string AlreadyExists = "{entity}.exists";     // chỉ cần nếu có business key + Add
}
```

`{entity}` ở error code viết thường, không dấu.

---

## Command: Add

`Features/{Entity}/Commands/Add{Entity}/Add{Entity}Command.cs`

```csharp
using YTTrending.Application.Features.{Entity}.Dtos;

namespace YTTrending.Application.Features.{Entity}.Commands.Add{Entity};

public sealed record Add{Entity}Command(string SomeField) : IRequest<Result<{Entity}Dto>>;
```

`Add{Entity}CommandHandler.cs` — 2 biến thể tuỳ có business key hay không:

```csharp
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.{Entity}.Dtos;

namespace YTTrending.Application.Features.{Entity}.Commands.Add{Entity};

public sealed class Add{Entity}CommandHandler(
    IUnitOfWork uow,
    I{Entity}Repository {entities})
    : IRequestHandler<Add{Entity}Command, Result<{Entity}Dto>>
{
    public async Task<Result<{Entity}Dto>> Handle(Add{Entity}Command cmd, CancellationToken ct)
    {
        // Chỉ giữ nhánh check trùng này nếu entity có business key:
        if (await {entities}.ExistsBySomeFieldAsync(cmd.SomeField, ct))
            return Result<{Entity}Dto>.Failure(Error.Conflict({Entity}Errors.AlreadyExists, "Đã tồn tại."));

        var entity = new {Entity} { SomeField = cmd.SomeField };
        {entities}.Create(entity);
        await uow.SaveChangesAsync(ct);

        return Result<{Entity}Dto>.Success(entity.ToDto());
    }
}
```

`Add{Entity}CommandValidator.cs`

```csharp
namespace YTTrending.Application.Features.{Entity}.Commands.Add{Entity};

public sealed class Add{Entity}CommandValidator : AbstractValidator<Add{Entity}Command>
{
    public Add{Entity}CommandValidator()
    {
        RuleFor(x => x.SomeField)
            .NotEmpty().WithMessage("SomeField is required.");
    }
}
```

---

## Command: Update

`Features/{Entity}/Commands/Update{Entity}/Update{Entity}Command.cs`

```csharp
using YTTrending.Application.Features.{Entity}.Dtos;

namespace YTTrending.Application.Features.{Entity}.Commands.Update{Entity};

public record Update{Entity}Command(int Id, string SomeField, bool IsEnabled) : IRequest<Result<{Entity}Dto>>;
```

`Update{Entity}CommandHandler.cs`

```csharp
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.{Entity}.Dtos;

namespace YTTrending.Application.Features.{Entity}.Commands.Update{Entity};

public class Update{Entity}CommandHandler(I{Entity}Repository {entities}, IUnitOfWork uow)
    : IRequestHandler<Update{Entity}Command, Result<{Entity}Dto>>
{
    public async Task<Result<{Entity}Dto>> Handle(Update{Entity}Command cmd, CancellationToken ct)
    {
        var entity = await {entities}.GetByIdAsync(cmd.Id, ct);
        if (entity is null)
            return Result<{Entity}Dto>.Failure(Error.NotFound({Entity}Errors.NotFound, "Không tìm thấy với id được cung cấp."));

        entity.SomeField = cmd.SomeField;  // EF tự phát hiện thay đổi
        entity.IsEnabled = cmd.IsEnabled;
        await uow.SaveChangesAsync(ct);

        return Result<{Entity}Dto>.Success(entity.ToDto());
    }
}
```

`Update{Entity}CommandValidator.cs`

```csharp
namespace YTTrending.Application.Features.{Entity}.Commands.Update{Entity};

public sealed class Update{Entity}CommandValidator : AbstractValidator<Update{Entity}Command>
{
    public Update{Entity}CommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("Id must be greater than 0.");
        RuleFor(x => x.SomeField).NotEmpty().WithMessage("SomeField is required.");
    }
}
```

---

## Command: Delete

`Features/{Entity}/Commands/Delete{Entity}/Delete{Entity}Command.cs`

```csharp
namespace YTTrending.Application.Features.{Entity}.Commands.Delete{Entity};

public record Delete{Entity}Command(int Id) : IRequest<Result>;
```

`Delete{Entity}CommandHandler.cs`

```csharp
using YTTrending.Application.Common.Interfaces;

namespace YTTrending.Application.Features.{Entity}.Commands.Delete{Entity};

public sealed class Delete{Entity}CommandHandler(I{Entity}Repository {entities}, IUnitOfWork uow)
    : IRequestHandler<Delete{Entity}Command, Result>
{
    public async Task<Result> Handle(Delete{Entity}Command cmd, CancellationToken ct)
    {
        var entity = await {entities}.GetByIdAsync(cmd.Id, ct);
        if (entity is null)
            return Result.Failure(Error.NotFound({Entity}Errors.NotFound, "Không tìm thấy với id được cung cấp."));

        {entities}.Delete(entity);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
```

Không cần validator riêng cho Delete (chỉ có `Id`, kiểm tra tồn tại đã nằm trong handler).

---

## Query: GetById

`Features/{Entity}/Queries/Get{Entity}ById/Get{Entity}ByIdQuery.cs`

```csharp
using YTTrending.Application.Features.{Entity}.Dtos;

namespace YTTrending.Application.Features.{Entity}.Queries.Get{Entity}ById;

public record Get{Entity}ByIdQuery(int Id) : IRequest<Result<{Entity}Dto>>;
```

`Get{Entity}ByIdQueryHandler.cs`

```csharp
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.{Entity}.Dtos;

namespace YTTrending.Application.Features.{Entity}.Queries.Get{Entity}ById;

public sealed class Get{Entity}ByIdQueryHandler(I{Entity}Repository {entities})
    : IRequestHandler<Get{Entity}ByIdQuery, Result<{Entity}Dto>>
{
    public async Task<Result<{Entity}Dto>> Handle(Get{Entity}ByIdQuery q, CancellationToken ct)
    {
        var entity = await {entities}.GetByIdAsync(q.Id, ct);
        if (entity is null)
            return Result<{Entity}Dto>.Failure(Error.NotFound({Entity}Errors.NotFound, $"{Entity} with ID {{q.Id}} not found."));
        return Result<{Entity}Dto>.Success(entity.ToDto());
    }
}
```

---

## Query: GetPaged (list)

`Common/Models/{Entity}Filter.cs`

```csharp
namespace YTTrending.Application.Common.Models;

public record {Entity}Filter : PagedQuery;
// Thêm field filter optional ở đây nếu bước 1 xác nhận cần, vd:
// public bool? IsEnabled { get; init; }
```

`Features/{Entity}/Queries/Get{Entities}/Get{Entities}Query.cs`

```csharp
using YTTrending.Application.Features.{Entity}.Dtos;

namespace YTTrending.Application.Features.{Entity}.Queries.Get{Entities};

public record Get{Entities}Query : {Entity}Filter, IRequest<Result<PagedResult<{Entity}Dto>>>;
```

`Get{Entities}QueryHandler.cs`

```csharp
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.{Entity}.Dtos;

namespace YTTrending.Application.Features.{Entity}.Queries.Get{Entities};

public sealed class Get{Entities}QueryHandler(I{Entity}Repository {entities})
    : IRequestHandler<Get{Entities}Query, Result<PagedResult<{Entity}Dto>>>
{
    public async Task<Result<PagedResult<{Entity}Dto>>> Handle(Get{Entities}Query q, CancellationToken ct)
    {
        var result = await {entities}.GetPagedAsync(q, ct);
        var dtos = result.Items.Select(e => e.ToDto()).ToList();
        return Result<PagedResult<{Entity}Dto>>.Success(
            new PagedResult<{Entity}Dto>(dtos, result.Page, result.PageSize, result.TotalCount));
    }
}
```

---

## Repository interface

`Common/Interfaces/I{Entity}Repository.cs`

```csharp
namespace YTTrending.Application.Common.Interfaces;

public interface I{Entity}Repository : IRepository<{Entity}>
{
    // Chỉ giữ dòng nào tương ứng operation được chọn:
    Task<bool> ExistsBySomeFieldAsync(string someField, CancellationToken ct); // cần business key + Add
    Task<PagedResult<{Entity}>> GetPagedAsync({Entity}Filter filter, CancellationToken ct); // cần GetPaged
}
```

Nếu chỉ có GetById/Update/Delete mà không có Add/GetPaged, interface có thể rút gọn thành `public interface I{Entity}Repository : IRepository<{Entity}>` (rỗng — kế thừa đủ `GetByIdAsync`/`Create`/`Delete`).

---

## Repository implementation

`Infrastructure/Persistence/Repositories/{Entity}Repository.cs`

```csharp
using YTTrending.Application.Common.Extensions;
using YTTrending.Application.Common.Models;

namespace YTTrending.Infrastructure.Persistence.Repositories;

public sealed class {Entity}Repository(YTTrendingDbContext db)
    : Repository<{Entity}>(db), I{Entity}Repository
{
    public Task<bool> ExistsBySomeFieldAsync(string someField, CancellationToken ct) =>
        Set.AnyAsync(e => e.SomeField == someField, ct);

    public Task<PagedResult<{Entity}>> GetPagedAsync({Entity}Filter filter, CancellationToken ct)
        => Set.AsNoTracking()
            .OrderByDescending(e => e.Id) // đổi sang cột thật sự ổn định của entity nếu có (vd CreatedAt); LUÔN giữ .ThenBy(x => x.Id)
            .ThenBy(e => e.Id)
            // .WhereIf(filter.IsEnabled.HasValue, e => e.IsEnabled == filter.IsEnabled)  — filter optional nếu có
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);
}
```

Lưu ý: nếu `OrderByDescending` đã chọn đúng là `Id` thì không cần `.ThenBy` nữa (đã là khóa duy nhất); nhưng nếu order theo cột khác (CreatedAt, tên, v.v.) thì `.ThenBy(x => x.Id)` là bắt buộc.

---

## EF configuration

`Infrastructure/Persistence/Configurations/{Entity}Configuration.cs`

```csharp
namespace YTTrending.Infrastructure.Persistence.Configurations;

public class {Entity}Configuration : IEntityTypeConfiguration<{Entity}>
{
    public void Configure(EntityTypeBuilder<{Entity}> builder)
    {
        builder.Property(e => e.SomeField).HasMaxLength(255); // set maxlength thật theo field string thật

        // Chỉ thêm nếu có business key:
        builder.HasIndex(e => e.SomeField).IsUnique();
    }
}
```

Không cần đăng ký `IEntityTypeConfiguration` thủ công nếu `YTTrendingDbContext` đang dùng `ApplyConfigurationsFromAssembly` — kiểm tra `OnModelCreating` trong `YTTrendingDbContext.cs` trước; nếu context đang liệt kê từng `ApplyConfiguration<T>()` thủ công thì phải thêm dòng tương ứng.

---

## Controller

`API/Controllers/{Entities}Controller.cs`

```csharp
using YTTrending.Application.Features.{Entity}.Commands.Add{Entity};
using YTTrending.Application.Features.{Entity}.Commands.Delete{Entity};
using YTTrending.Application.Features.{Entity}.Commands.Update{Entity};
using YTTrending.Application.Features.{Entity}.Queries.Get{Entity}ById;
using YTTrending.Application.Features.{Entity}.Queries.Get{Entities};

namespace YTTrending.API.Controllers;

[ApiController]
[Route("api/{entities}")]
public sealed class {Entities}Controller(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Add{Entity}Command command, CancellationToken ct)
        => (await sender.Send(command, ct)).ToActionResult();

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Get{Entities}Query query, CancellationToken ct)
        => (await sender.Send(query, ct)).ToActionResult();

    [HttpGet("{{id:int}}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => (await sender.Send(new Get{Entity}ByIdQuery(id), ct)).ToActionResult();

    [HttpPut("{{id:int}}")]
    public async Task<IActionResult> Edit(int id, [FromBody] Update{Entity}Command command, CancellationToken ct)
        => (await sender.Send(command with { Id = id }, ct)).ToActionResult();

    [HttpDelete("{{id:int}}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => (await sender.Send(new Delete{Entity}Command(id), ct)).ToActionResult();
}
```

Chỉ giữ action tương ứng operation được chọn — using ở đầu file cũng chỉ import namespace của operation đang có.

---

## Wiring cần nhắc user tự làm (không tự sinh)

`Infrastructure/DependencyInjection.cs` — thêm dòng:

```csharp
services.AddScoped<I{Entity}Repository, {Entity}Repository>();
```

Migration:

```
dotnet ef migrations add Add{Entity} -p src/YTTrending.Infrastructure -s src/YTTrending.API
dotnet ef database update
```
