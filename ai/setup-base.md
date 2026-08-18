# Setup Base — Checklist

> Danh sách những thứ **cần setup** trước khi viết feature. Chỉ liệt kê *cái gì*, không nói *làm thế nào*.
> Cách làm / code mẫu / cạm bẫy: [`setup-base-notes.md`](setup-base-notes.md) — mở đúng mục khi bắt tay vào làm.
> Quyết định đã chốt: [`../docs/decisions.md`](../docs/decisions.md) mục "Setup base".

## Base xong = 2 điều kiện

- `dotnet run` lên được, Swagger mở được, DB tạo từ migration
- `POST /api/channels` qua Swagger → có row trong DB (chứng minh 4 layer thông nhau)

Chưa cần: YouTube API thật, job chạy thật, trending score, test project.

---

## 1. Nền solution ✅ XONG

- [x] Database `yttrending_dev` trên Postgres local (kèm `yttrending_test`)
- [x] `Directory.Packages.props` — pin version tập trung
- [x] `Directory.Build.props` — nullable warning as error
- [x] `.editorconfig`
- [x] `.gitignore` — `logs/` đã được `[Ll]ogs/` phủ sẵn, không cần thêm
- [x] Cài package cho 4 project
- [x] Xóa 3 file `Class1.cs`
- [x] `GlobalUsings.cs` mỗi project

## 2. Domain

- [x] Enum `VideoStatus`
- [x] `AuditableEntity` (base class cho `Channel`, `Video`)
- [x] 5 entity: `Channel`, `Video`, `VideoMetricSnapshot`, `TrendingScore`, `SavedIdea`
- [x] Invariant ở `Application/Common/VideoStateRules.cs` (entity anemic): `Archive()` chặn terminal-state + set `ArchivedAt`, `StartTracking()` chỉ từ NEW

## 3. Application — khối dùng chung ✅ XONG

- [x] `Result` / `Result<T>` / `Error` (có `ErrorType` + lỗi nhiều field)
- [x] `PagedResult<T>` + `PagedQuery` (có cap page size)
- [x] `QueryableExtensions` — `ToPagedResultAsync`, `WhereIf`
- [x] `IYTTrendingDbContext` — làm sớm ở mục 4 vì `YTTrendingDbContext` cần implement nó
- [x] `IYouTubeClient`
- [x] `LoggingBehavior` + `ValidationBehavior`
- [x] Options: `TrackingOptions`, `TrendingOptions`, `JobOptions` (validate on start)
- [x] `AddApplication()`

## 4. Infrastructure — Persistence ✅ XONG

- [x] `YTTrendingDbContext` (kèm audit tự động lúc SaveChanges)
- [x] 5 `IEntityTypeConfiguration` — unique index, query filter soft-delete, enum → string
- [x] `AddInfrastructure()` — DbContext + snake_case + `TimeProvider` **(chưa bind Options — chờ mục 3)**
- [x] Migration `InitialCreate` + apply
- [x] Auto-migrate lúc startup (có cờ bật/tắt)

## 5. API — wiring ✅ XONG

- [x] Serilog + file sink
- [x] Pipeline: exception handler → request logging → CORS → controllers
- [x] CORS cho Angular dev server
- [x] JSON: camelCase + enum trả string
- [x] `ResultExtensions` — map `Result` → HTTP status
- [x] `GlobalExceptionHandler`
- [x] `appsettings.json` đủ section + user-secrets cho connection string & API key
- [x] Port cố định trong `launchSettings.json`

## 6. Slice nghiệm thu

- [ ] `AddChannelCommand` + validator
- [ ] `GetChannelsQuery` (có paging)
- [ ] `FakeYouTubeClient`
- [ ] `ChannelsController`
- [ ] Seed data cho Development

## 7. Khung background job

- [ ] `SyncChannelJob` — kill-switch, try/catch, chống chạy chồng
- [ ] `SyncChannelsCommand` (rỗng, chỉ log)
- [ ] `JobsController` — chạy tay
- [ ] README cách chạy

---

## Hoãn sang phase sau

Test project · YouTube client thật · full-text search · health check · rate limiting · caching · Docker hóa API · bảng `app_config`
