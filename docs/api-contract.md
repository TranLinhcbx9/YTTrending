# API Contract — cho FE (Angular, repo riêng)

> Tài liệu hợp đồng JSON **duy nhất, chi tiết** giữa backend và FE — đọc trực tiếp từ code thật, không suy đoán. Quy tắc tóm tắt ở [`coding-convention.md`](coding-convention.md) mục 11 chỉ trỏ về đây. Đổi shape DTO/endpoint ở backend → **phải cập nhật file này cùng lúc** (SSOT, xem [`../AGENTS.md`](../AGENTS.md)).

## 1. Base URL & môi trường

- Dev: `http://localhost:5118` (chạy `dotnet run --project src/YTTrending.API`), Swagger tại `/swagger`.
- **Không có auth** — Phase 1 single-user, không header nào bắt buộc.
- **CORS chỉ bật ở Development** (`Program.cs`, `UseCors` nằm trong `if (app.Environment.IsDevelopment())`) — origin cho phép đọc từ config `Cors:AllowedOrigins`, hiện là `http://localhost:4200` (`appsettings.json`). **Production chưa cấu hình CORS** — gap Phase 1, cần biết trước khi deploy.

## 2. Quy ước JSON chung

- Property JSON: **camelCase** (default của System.Text.Json, không set tường minh trong code).
- Enum trả về **string, giữ nguyên giá trị PascalCase** — vd `"status": "Tracking"`, **không phải** `"tracking"` (qua `JsonStringEnumConverter`, `Program.cs`).
- Giá trị `null` vẫn xuất hiện trong JSON (không set `IgnoreNullValues`) — vd `"lastSyncAt": null`.

## 3. Response thành công

| Kiểu Result handler trả | HTTP status | Body |
|---|---|---|
| `Result` (không có giá trị, vd Delete) | **204 No Content** | rỗng |
| `Result<T>` thành công | **200 OK** | `T` trực tiếp — **không** bọc envelope |

⚠️ Kể cả endpoint tạo mới (`POST /api/channels`) cũng trả **200**, không phải 201 — mọi `Result<T>` thành công đều map `OkObjectResult` (`ResultExtensions.cs`), không phân biệt create/read.

## 4. Response lỗi — `ProblemDetails` (RFC 7807)

Lỗi nghiệp vụ (`Result.Failure`) và lỗi validation đều map qua `Error.ToProblemDetails()` thành `ProblemDetails`/`ValidationProblemDetails` chuẩn ASP.NET Core. Field trên wire: `status`, `title`, `detail`, `code` (custom extension) — **không có** field `type`, **không có** field `message` (là `detail`).

### 4.1. Lỗi thường (NotFound / Conflict)

```json
{
  "status": 404,
  "title": "Not Found",
  "detail": "Channel with ID 99 not found.",
  "code": "channel.notFound"
}
```

### 4.2. Lỗi validation (FluentValidation, tự động qua `ValidationBehavior`)

Đây là `ValidationProblemDetails` — dict lỗi theo field nằm ở key **`errors`** (chuẩn ASP.NET Core), **không phải `fields`**. Tên field trong `errors` là **camelCase**.

```json
{
  "status": 400,
  "title": "Validation failed",
  "detail": "Dữ liệu không hợp lệ",
  "code": "validation.failed",
  "errors": {
    "youtubeHandle": ["YoutubeHandle is required."]
  }
}
```

### 4.3. Lỗi hệ thống chưa lường trước (500)

Không đi qua `Error`/`ErrorType` — do `GlobalExceptionHandler` xử lý riêng khi có exception chưa bắt.

```json
{
  "status": 500,
  "title": "Server Error",
  "detail": "Đã có lỗi xảy ra, vui lòng thử lại.",
  "code": "server.error"
}
```

### 4.4. Bảng map `ErrorType` → HTTP status

| `ErrorType` | HTTP status | `title` |
|---|---|---|
| `Validation` | 400 | `Validation failed` |
| `NotFound` | 404 | `Not Found` |
| `Conflict` | 409 | `Conflict` |

## 5. Phân trang

### Request — query params (`PagedQuery` base, `Common/Models/PagedQuery.cs`)

| Param | Default | Giới hạn |
|---|---|---|
| `page` | `1` | `< 1` → tự clamp về `1` (không lỗi) |
| `pageSize` | `20` | `< 1` → về 20; `> 100` → tự clamp về `100` (không lỗi) |

### Response — `PagedResult<T>`

```json
{
  "items": [ /* T[] */ ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 47,
  "totalPages": 3,
  "hasNext": true
}
```

⚠️ Chỉ có `hasNext` — **không có `hasPrevious`**.

## 6. Endpoint hiện có

### Channels — `api/channels`

| Verb | Route | Request | Response thành công |
|---|---|---|---|
| POST | `/api/channels` | body `AddChannelCommand` | 200, `ChannelDto` |
| GET | `/api/channels` | query `GetChannelsQuery` (= `page`, `pageSize`) | 200, `PagedResult<ChannelDto>` |
| GET | `/api/channels/{id}` | route `id` | 200, `ChannelDto` |
| PUT | `/api/channels/{id}` | route `id` + body `UpdateChannelCommand` (`id` trong body bị route ghi đè) | 200, `ChannelDto` |
| DELETE | `/api/channels/{id}` | route `id` | 204 |

### Videos — `api/videos`

| Verb | Route | Request | Response thành công |
|---|---|---|---|
| GET | `/api/videos` | query `GetVideosQuery` (= `VideoFilter`: `channelId?`, `status?` + `page`, `pageSize`) | 200, `PagedResult<VideoDto>` |
| GET | `/api/videos/{id}` | route `id` | 200, `VideoDto` |

Chưa có create/update/delete cho Video — video do background job tạo/cập nhật (chưa build ở Phase 1 hiện tại, xem [`../ai/current.md`](../ai/current.md)), không phải do FE gọi API tạo. Endpoint detail dùng **chung** `VideoDto` với endpoint list — không có `VideoDetailDto` riêng.

## 7. DTO & request body

### `ChannelDto`

```ts
{
  id: number;
  youtubeChannelId: string;
  name: string;
  url: string;
  isEnabled: boolean;
  lastSyncAt: string | null;   // DateTimeOffset ISO 8601
  createdAt: string;           // DateTimeOffset ISO 8601
}
```

### `VideoDto`

```ts
{
  id: number;
  youtubeVideoId: string;
  channelId: number;
  channelName: string;
  title: string;
  publishedAt: string;         // DateTimeOffset ISO 8601
  durationSeconds: number;
  thumbnailUrl: string | null;
  status: "New" | "Tracking" | "Archived";
  latestViews: number;
  latestLikes: number;
  latestComments: number;
}
```

### Request body — `AddChannelCommand` (POST `/api/channels`)

```json
{ "youtubeHandle": "@MrBeast" }
```
Input là **YouTube handle** (`@name`, có hoặc không có dấu "@" đều được — server tự chuẩn hoá), **không phải channel ID**. Server resolve qua YouTube để lấy `youtubeChannelId` chuẩn trước khi lưu.
Validate: `youtubeHandle` bắt buộc (`NotEmpty`).

### Request body — `UpdateChannelCommand` (PUT `/api/channels/{id}`)

```json
{ "id": 1, "name": "...", "url": "https://...", "isEnabled": true }
```
Validate: `id > 0`, `name` bắt buộc, `url` bắt buộc + phải là absolute URL hợp lệ (`Uri.IsWellFormedUriString`). `id` trong body không cần khớp route (bị override).

### Query params — `GET /api/videos`

`channelId` (optional, số), `status` (optional, `New`/`Tracking`/`Archived`), cộng `page`/`pageSize` (mục 5).

## 8. Enum `VideoStatus`

`New` → `Tracking` → `Archived`. **`Archived` là trạng thái cuối** — không có đường quay lại `Tracking` (invariant toàn dự án, xem [`../AGENTS.md`](../AGENTS.md)).

## 9. Error code đã dùng

| Code | Sinh ra khi | HTTP |
|---|---|---|
| `channel.notFound` | Không tìm thấy channel (theo id, hoặc theo YoutubeChannelId khi add) | 404 |
| `channel.exists` | Add channel trùng (đã theo dõi rồi) | 409 |
| `video.notFound` | Không tìm thấy video theo id | 404 |
| `validation.failed` | FluentValidation fail (mọi command có validator) | 400 |
| `server.error` | Exception chưa lường trước | 500 |

## 10. Gaps Phase 1 — FE cần biết trước

- Không auth, không phân quyền.
- CORS chỉ hoạt động ở Development — chưa có cấu hình cho production.
- Không có `VideoDetailDto` riêng biệt — trang detail phải tự đủ dùng với `VideoDto`.
- `POST` tạo resource trả `200`, không phải `201` — đừng dựa vào status code để phân biệt create/read.
- Video: chỉ có Query (list/detail), chưa có Command (add/update/delete) — video hiện tại trong DB là seed giả (`DevDataSeeder`), backend job đồng bộ thật chưa build.
</content>
