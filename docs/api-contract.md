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

⚠️ Kể cả endpoint tạo mới (`POST /api/channels`) cũng trả **200**, không phải 201 — các
`Result<T>` thành công mặc định map `OkObjectResult` (`ResultExtensions.cs`), không phân biệt
create/read. Ngoại lệ được ghi rõ ở endpoint: `POST /api/jobs/sync` trả **202 Accepted** để báo
request đã được enqueue xử lý bất đồng bộ.

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

### Request — query params (`PagedQuery` base, `Common/Models/Pagination/PagedQuery.cs`)

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
| POST | `/api/channels/{id}/sync` | route `id`, không body | 200, `SyncChannelResultDto` |

### Jobs — sync runs — `api/jobs`

Sync-all chạy bất đồng bộ qua một `SyncRun`. Request tạo run chỉ snapshot các channel đang bật,
lưu xong thì enqueue cho worker nội bộ và trả ngay; FE dùng `GET` để poll summary, rồi đọc danh
sách item qua endpoint paged riêng. Worker chỉ xử lý một run và các item của run đó theo tuần tự.

| Verb | Route | Request | Response thành công |
|---|---|---|---|
| POST | `/api/jobs/sync` | Không body; trigger luôn là `Manual` | **202**, `SyncRunDto` |
| GET | `/api/jobs/sync/{id}` | route `id` | 200, `SyncRunDto` |
| GET | `/api/jobs/sync/{id}/items` | route `id` + query `page`, `pageSize`, `status?` | 200, `PagedResult<SyncRunItemDto>` |

`POST /api/jobs/sync` trả `409` với code `syncRun.inProgress` nếu đã có run ở trạng thái
`Pending` hoặc `Running`, và trả `409` với code `syncRun.noEnabledChannels` nếu không có channel
đang bật. `GET` với `id <= 0` trả validation `400`; không tìm thấy run trả `404` với code
`syncRun.notFound`. Các route item cũng trả cùng lỗi `syncRun.notFound` nếu run không tồn tại.

### Query params — `GET /api/jobs/sync/{id}/items`

- `page`, `pageSize`: theo mục 5.
- `status` (optional): `Pending` / `Running` / `Succeeded` / `Skipped` / `Failed`.
- `id` trên route là nguồn duy nhất của `syncRunId`; query string không ghi đè được route.
- Item được sắp xếp ổn định theo `id` tăng dần.

### Vòng đời và recovery của SyncRun

Sau response `202`, run có thể đang là `Pending` rồi chuyển rất nhanh sang `Running`; không suy
ra progress từ riêng response tạo run mà hãy poll summary. Các trạng thái terminal của run là
`Completed`, `CompletedWithIssues`, `Interrupted` và `Failed`.

| Run status | Ý nghĩa cho FE |
|---|---|
| `Pending` | Đã lưu và đã enqueue, worker chưa bắt đầu. Tiếp tục poll. |
| `Running` | Worker đang sync tuần tự từng channel. Tiếp tục poll. |
| `Completed` | Mọi item đều `Succeeded`. Dừng poll. |
| `CompletedWithIssues` | Mọi item đã kết thúc nhưng có ít nhất một `Skipped` hoặc `Failed`. Dừng poll, mở bảng kết quả. |
| `Interrupted` | Ứng dụng đã restart/dừng khi run còn dang dở. Backend **không resume** run này; dừng poll và cho người dùng tạo run mới. |
| `Failed` | Processor gặp lỗi cấp run. Dừng poll và hiển thị `errorMessage` cấp run nếu có. |

Khi API khởi động, mọi run còn `Pending`/`Running` được mark `Interrupted`; item chưa kết thúc
được mark `Skipped` với `errorCode` là `syncRun.interrupted`. Queue chỉ nằm trong memory nên không
có endpoint hay cơ chế FE dùng để resume run cũ.

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
  uploadsPlaylistId: string | null;
  isEnabled: boolean;
  lastSyncAt: string | null;   // DateTimeOffset ISO 8601
  createdAt: string;           // DateTimeOffset ISO 8601
}
```

### `VideoDto`

```ts
{
  id: number;
  youtubeVideoUrl: string;
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

### Request — `POST /api/channels/{id}/sync`

Đồng bộ Shorts của một channel ngay lập tức; không có request body. Thành công trả `200 OK` với các count để FE tự quyết định wording/toast. Endpoint dùng YouTube Data API, chọn tối đa số Shorts qualify mới nhất trong `RecentDays`, tạo candidate `NEW`, promote candidate `NEW` từ lượt trước sang `TRACKING`, cập nhật metadata của video active được chọn, archive video active đã ra khỏi tracking window và cập nhật `lastSyncAt`.

```json
{
  "fetchedShortsCount": 32,
  "qualifiedShortsCount": 20,
  "newlyDiscoveredCount": 5,
  "newlyTrackedCount": 3,
  "existingVideosRefreshedCount": 15,
  "archivedVideosCount": 2
}
```

### `SyncRunDto`

```ts
{
  id: number;
  triggerType: "Manual" | "Scheduled";
  status: "Pending" | "Running" | "Completed" | "CompletedWithIssues" | "Interrupted" | "Failed";
  totalCount: number;
  successCount: number;
  skippedCount: number;
  failedCount: number;
  processedCount: number;      // successCount + skippedCount + failedCount
  errorCode: string | null;
  errorMessage: string | null;
  createdAt: string;           // DateTimeOffset ISO 8601
  startedAt: string | null;    // DateTimeOffset ISO 8601
  completedAt: string | null;  // DateTimeOffset ISO 8601
}
```

### `SyncRunItemDto`

```ts
{
  id: number;
  syncRunId: number;
  channelId: number;
  channelName: string;         // snapshot tại lúc tạo run
  status: "Pending" | "Running" | "Succeeded" | "Skipped" | "Failed" | null;
  errorCode: string | null;
  errorMessage: string | null;
  startedAt: string | null;    // DateTimeOffset ISO 8601
  completedAt: string | null;  // DateTimeOffset ISO 8601
}
```

Ý nghĩa `SyncRunItemDto.status`: `Succeeded`, `Skipped` và `Failed` là terminal. `Skipped` dùng
cho lỗi nghiệp vụ dự kiến của một channel (ví dụ đang sync, cooldown, hoặc channel đã bị xoá);
`Failed` dùng cho lỗi thực thi/exception ở channel đó. DTO khai báo `status` nullable trên wire,
nhưng item được tạo bởi backend hiện tại luôn có một status; FE vẫn nên render fallback cho `null`.

Khi channel đang được một request khác sync, server trả `409` với code `channel.syncInProgress`; khi chưa hết cooldown thủ công, trả `409` với code `channel.syncTooSoon`. `id <= 0` trả validation error `400`; id không tồn tại trả `404` với code `channel.notFound`.

### Query params — `GET /api/videos`

- `channelIds` (optional, mảng số): lọc theo một hoặc nhiều channel. Gửi query key lặp lại, ví dụ `?channelIds=1&channelIds=3`.
- `status` (optional): `New` / `Tracking` / `Archived`.
- `minViews` (optional, số nguyên 64-bit): chỉ lấy video có `latestViews >= minViews`.
- `timeRanges` (optional, số ngày): chỉ lấy video có `publishedAt` trong N ngày gần nhất. Tên param trên wire hiện là số nhiều: `timeRanges`.
- `page`, `pageSize`: theo mục 5.

## 8. Enum `VideoStatus`

`New` → `Tracking` → `Archived`. **`Archived` là trạng thái cuối** — không có đường quay lại `Tracking` (invariant toàn dự án, xem [`../AGENTS.md`](../AGENTS.md)).

## 9. Error code đã dùng

| Code | Sinh ra khi | HTTP |
|---|---|---|
| `channel.notFound` | Không tìm thấy channel (theo id, hoặc theo YoutubeChannelId khi add) | 404 |
| `channel.exists` | Add channel trùng (đã theo dõi rồi) | 409 |
| `channel.syncInProgress` | Có request sync khác đang chạy cho cùng channel | 409 |
| `channel.syncTooSoon` | Channel chưa hết cooldown sync thủ công | 409 |
| `video.notFound` | Không tìm thấy video theo id | 404 |
| `syncRun.inProgress` | Đã có một SyncRun đang Pending/Running | 409 |
| `syncRun.noEnabledChannels` | Không có channel đang bật để tạo SyncRun | 409 |
| `syncRun.notFound` | Không tìm thấy SyncRun theo id | 404 |
| `syncRun.interrupted` | App restart/dừng, xuất hiện trong `errorCode` của run/item bị ngắt | — (body thành công) |
| `syncRun.channelExecutionFailed` | Exception khi xử lý một channel, xuất hiện trong `errorCode` của item `Failed` | — (body thành công) |
| `syncRun.executionFailed` | Lỗi cấp processor, xuất hiện ở run `Failed` và các item còn dang dở bị `Skipped` | — (body thành công) |
| `validation.failed` | FluentValidation fail (mọi command có validator) | 400 |
| `server.error` | Exception chưa lường trước | 500 |

## 10. Gaps Phase 1 — FE cần biết trước

- Không auth, không phân quyền.
- CORS chỉ hoạt động ở Development — chưa có cấu hình cho production.
- Không có `VideoDetailDto` riêng biệt — trang detail phải tự đủ dùng với `VideoDto`.
- `POST` tạo resource trả `200`, không phải `201` — đừng dựa vào status code để phân biệt create/read.
- Video: FE chỉ có Query (list/detail), không có Command (add/update/delete). Video được tạo/cập nhật từ `POST /api/channels/{id}/sync`; SyncRun worker xử lý Sync all theo polling API ở trên. Metrics Update Job vẫn chưa expose API cho FE.
- SyncRun chưa có endpoint list history, cancel, retry hay resume. `POST /api/jobs/sync` chỉ tạo run mới; nếu nhận `syncRun.inProgress`, response không mang id của run đang chạy để FE chuyển sang theo dõi.
</content>
