# Sổ quyết định kỹ thuật

Tài liệu này chỉ lưu quyết định kỹ thuật đang có hiệu lực, lý do, đánh đổi và hệ quả. Quy tắc nghiệp vụ, cấu hình, schema và hợp đồng API thuộc các tài liệu nguồn được dẫn ở mỗi mục. ID không được đổi hoặc tái sử dụng.

`Đã chốt` là lựa chọn đã được ghi nhận. `Cần xác nhận` đánh dấu nơi code, tài liệu hoặc artifact hiện có không đủ để khẳng định lựa chọn vẫn đúng; mục đó không tự đặt ra kiến trúc mới.

## Đã chốt

### DEC-001 — Kiến trúc bốn tầng và CQRS tối giản

**Status:** Đã chốt

**References:** [`architecture.md`](architecture.md), [`coding-convention.md`](coding-convention.md)

**Bối cảnh:** API cần tách rule nghiệp vụ, persistence, tích hợp YouTube và HTTP transport.

**Quyết định:** Dùng Domain / Application / Infrastructure / API; request đi qua MediatR. Controller chỉ điều phối request/response, còn job nền chỉ kích hoạt use case Application.

**Lý do:** Giữ dependency một chiều và không để HTTP hay provider persistence đi vào rule nghiệp vụ.

**Trade-off:** Thêm lớp và interface so với gọi DbContext trực tiếp từ controller.

**Hệ quả:** Thay đổi cấu trúc layer hoặc trách nhiệm controller phải đối chiếu [`architecture.md`](architecture.md) và [`coding-convention.md`](coding-convention.md).

**Xem xét lại khi:** Có yêu cầu làm thay đổi ranh giới layer hoặc cách tổ chức use case.

### DEC-002 — Runtime .NET 8

**Status:** Đã chốt

**References:** [`../Directory.Build.props`](../Directory.Build.props), [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)

**Bối cảnh:** Các project hiện target `net8.0` và package EF Core/Npgsql đang ở dòng 8.x.

**Quyết định:** Giữ .NET 8 cho phạm vi hiện tại; nâng runtime là một quyết định migration riêng.

**Lý do:** Đổi runtime phải đánh giá đồng thời package, deployment và môi trường chạy.

**Trade-off:** .NET 8 kết thúc hỗ trợ ngày 10/11/2026; tiếp tục sau mốc đó mất bản vá và hỗ trợ chính thức.

**Hệ quả:** Bất kỳ nâng runtime nào phải rà tương thích toàn bộ solution và artifact triển khai.

**Xem xét lại khi:** Trước ngày kết thúc hỗ trợ, hoặc khi một dependency/deployment không còn tương thích.

### DEC-003 — MediatR giữ ở dòng 12.x

**Status:** Đã chốt

**References:** [`../Directory.Packages.props`](../Directory.Packages.props), [MediatR licensing](https://mediatr.io/)

**Bối cảnh:** Application đang pin MediatR 12.4.1; MediatR 13 trở lên có mô hình license thương mại.

**Quyết định:** Không nâng major MediatR nếu chưa đánh giá và chấp thuận điều kiện license cùng ảnh hưởng migration.

**Lý do:** Tránh thay đổi chi phí và điều khoản sử dụng ngầm trong một cập nhật dependency.

**Trade-off:** Không nhận thay đổi của major mới cho đến khi hoàn tất đánh giá.

**Hệ quả:** Cập nhật package trong dòng 12.x và nâng major là hai việc khác nhau.

**Xem xét lại khi:** Có nhu cầu thực tế từ major mới hoặc điều kiện license thay đổi.

### DEC-004 — Cấu hình qua Options, không có `app_config`

**Status:** Đã chốt

**References:** [`config.md`](config.md), [`database.md`](database.md), [`../src/YTTrending.Application/DependencyInjection.cs`](../src/YTTrending.Application/DependencyInjection.cs)

**Bối cảnh:** Interval, ngưỡng và trọng số cần thay đổi mà không sửa rule; schema hiện không có bảng cấu hình runtime.

**Quyết định:** Đọc cấu hình từ `appsettings` qua Options, validate khi khởi động; không tạo `app_config` cho đến khi có quyết định về chỉnh config lúc runtime.

**Lý do:** Mỗi thông số có một nguồn sở hữu rõ ràng và lỗi cấu hình được phát hiện trước khi job chạy.

**Trade-off:** Không có UI hay persistence cho thay đổi config lúc runtime.

**Hệ quả:** Giá trị, section và ngoại lệ secret thuộc [`config.md`](config.md); không lặp lại trong tài liệu này.

**Xem xét lại khi:** Có yêu cầu quản trị config runtime đã được xác nhận.

### DEC-005 — Thời gian qua `TimeProvider`

**Status:** Đã chốt

**References:** [`coding-convention.md`](coding-convention.md), [`database.md`](database.md), [`../src/YTTrending.Infrastructure/Persistence/YTTrendingDbContext.cs`](../src/YTTrending.Infrastructure/Persistence/YTTrendingDbContext.cs)

**Bối cảnh:** Cửa sổ tracking, cooldown, retention và mốc audit đều phụ thuộc thời gian.

**Quyết định:** Rule nghiệp vụ nhận `TimeProvider`; thời gian audit do DbContext điền khi save, còn thời gian nghiệp vụ được set tại use case tạo hoặc đổi trạng thái.

**Lý do:** Tách đồng hồ khỏi logic và không lẫn audit time với thời điểm nghiệp vụ.

**Trade-off:** Caller phải chọn rõ mốc nào dùng cho từng lượt xử lý.

**Hệ quả:** Không được suy ra từ quyết định này rằng mọi record trong một batch luôn có cùng timestamp; bảo đảm cụ thể của SyncRun nằm ở DEC-015.

**Xem xét lại khi:** Có nguồn thời gian khác hoặc yêu cầu đồng bộ timestamp xuyên process.

### DEC-006 — Result cho lỗi nghiệp vụ, ProblemDetails ở HTTP boundary

**Status:** Đã chốt

**References:** [`architecture.md`](architecture.md), [`api-contract.md`](api-contract.md), [`../src/YTTrending.Application/Common/Models/Results/Result.cs`](../src/YTTrending.Application/Common/Models/Results/Result.cs), [`../src/YTTrending.API/Common/ResultExtensions.cs`](../src/YTTrending.API/Common/ResultExtensions.cs)

**Bối cảnh:** Handler, job và controller cần cùng phân biệt lỗi nghiệp vụ dự kiến với lỗi hạ tầng hoặc bug.

**Quyết định:** Handler trả `Result`/`Result<T>` cho validation, not-found và conflict; lỗi bất thường, lỗi hạ tầng và invariant sai vẫn dùng exception. API chuyển `Error` thành RFC 7807 ProblemDetails ở một boundary; success không dùng envelope chung.

**Lý do:** Giữ mã lỗi nghiệp vụ có kiểu và tránh biến exception thành luồng điều khiển bình thường.

**Trade-off:** Caller phải kiểm tra kết quả trước khi đọc value; đọc `Result<T>.Value` khi fail ném `InvalidOperationException`.

**Hệ quả:** Không thêm error shape riêng cho endpoint; map HTTP và JSON chi tiết thuộc [`api-contract.md`](api-contract.md).

**Xem xét lại khi:** Có loại lỗi nghiệp vụ mới cần contract hoặc transport mới không dùng HTTP.

### DEC-007 — Repository, Unit of Work và ranh giới transaction

**Status:** Đã chốt

**References:** [`architecture.md`](architecture.md), [`coding-convention.md`](coding-convention.md), [`../src/YTTrending.Infrastructure/Persistence/UnitOfWork.cs`](../src/YTTrending.Infrastructure/Persistence/UnitOfWork.cs), [`../src/YTTrending.Application/Features/Jobs/Commands/ProcessSyncRun/ProcessSyncRunCommandHandler.cs`](../src/YTTrending.Application/Features/Jobs/Commands/ProcessSyncRun/ProcessSyncRunCommandHandler.cs)

**Bối cảnh:** Command và query cần một ranh giới data-access thống nhất, nhưng không phải use case nào cũng cần transaction xuyên suốt.

**Quyết định:** Repository theo aggregate và `IUnitOfWork` là pattern data-access chuẩn. Không có `TransactionBehavior` toàn cục; mỗi use case quyết định các điểm save của mình.

**Lý do:** Repository tách contract Application khỏi DbContext cụ thể; một behavior transaction không thể làm atomic các lần save riêng biệt hoặc gọi YouTube API.

**Trade-off:** Thêm abstraction và phải chủ động thiết kế transaction khi use case có nhiều bước ghi.

**Hệ quả:** `SaveChangesAsync` không được mô tả như bảo đảm transaction tuyệt đối: semantics còn tùy relational provider và ambient transaction, và chỉ bao phủ một lần save. `ProcessSyncRun` chủ động save nhiều mốc để lưu tiến độ, không phải một transaction duy nhất.

**Xem xét lại khi:** Một use case cần atomicity qua nhiều thay đổi persistence hoặc cần phối hợp durable với side effect ngoài DB.

### DEC-008 — Phân trang offset qua contract dùng chung

**Status:** Đã chốt

**References:** [`coding-convention.md`](coding-convention.md), [`api-contract.md`](api-contract.md), [`../src/YTTrending.Application/Common/Models/Pagination/PagedQuery.cs`](../src/YTTrending.Application/Common/Models/Pagination/PagedQuery.cs)

**Bối cảnh:** Danh sách API cần giới hạn input và thứ tự ổn định giữa các trang.

**Quyết định:** Dùng `PagedQuery`/`PagedResult<T>` và filter object dùng chung; input page/pageSize được clamp. Query phải có `OrderBy` và tie-breaker duy nhất, thường là `Id`.

**Lý do:** `IQueryable` giữ projection linh hoạt, còn thứ tự ổn định phải được mô tả ở query thay vì suy diễn từ type.

**Trade-off:** Quy ước sort không được compiler cưỡng chế; offset pagination kém phù hợp khi dataset biến động lớn.

**Hệ quả:** Không có `ORDER BY` ổn định thì PostgreSQL có thể lặp hoặc bỏ item giữa các trang; giá trị clamp và shape response thuộc [`api-contract.md`](api-contract.md).

**Xem xét lại khi:** Cần cursor/keyset pagination hoặc query không thể bảo đảm thứ tự ổn định.

### DEC-009 — EF Core/PostgreSQL và migration sinh bởi EF

**Status:** Đã chốt

**References:** [`database.md`](database.md), [`coding-convention.md`](coding-convention.md), [`../src/YTTrending.API/Program.cs`](../src/YTTrending.API/Program.cs)

**Bối cảnh:** Schema dùng PostgreSQL nhưng cần mapping rõ ràng và lịch sử migration có thể áp dụng lặp lại.

**Quyết định:** Dùng Fluent mapping, snake_case, enum lưu chuỗi và FK khai tường minh khi convention không nhận ra quan hệ. Migration nằm trong Infrastructure và do EF tạo; thay đổi schema đã được áp dụng phải đi bằng migration mới, không sửa tay migration cũ.

**Lý do:** Giữ Domain không phụ thuộc EF và giữ lịch sử schema nhất quán giữa database đã áp dụng.

**Trade-off:** Migration tăng dần; reset database local chỉ là lựa chọn có chủ đích cho dữ liệu disposable, không phải cách bắt buộc để sửa migration đã áp dụng.

**Hệ quả:** `MigrateAsync()` là đường áp migration hiện có; chi tiết cột, index, FK và kiểu dữ liệu chỉ thuộc [`database.md`](database.md).

**Xem xét lại khi:** Đổi provider, thay chiến lược migration hoặc cần rollout schema không tương thích.

### DEC-010 — Video identity và lifecycle ở Application

**Status:** Đã chốt

**References:** [`domain/discovery-engine.md`](domain/discovery-engine.md), [`domain/video-lifecycle.md`](domain/video-lifecycle.md), [`coding-convention.md`](coding-convention.md)

**Bối cảnh:** Metadata YouTube có thể đổi, còn entity Domain được giữ anemic.

**Quyết định:** So sánh video theo `YoutubeVideoId`; title/thumbnail không là khóa. `Archived` là trạng thái cuối và rule chuyển trạng thái nằm trong `VideoStateRules` ở Application, không dùng DB trigger.

**Lý do:** ID do YouTube cấp ổn định hơn metadata; rule ở Application phù hợp lựa chọn entity anemic.

**Trade-off:** Terminal-state là quy ước, không phải ràng buộc compiler hay database; gán trực tiếp property vẫn có thể compile.

**Hệ quả:** Flow NEW/TRACKING/ARCHIVED và cleanup chỉ có nguồn chi tiết ở các domain docs.

**Xem xét lại khi:** Domain cần behavior phong phú hơn hoặc terminal-state phải được cưỡng chế ở persistence.

### DEC-011 — Trending score là điểm hiện thời theo lượt tính

**Status:** Đã chốt — chưa có Metrics Update implementation

**References:** [`domain/trending-engine.md`](domain/trending-engine.md), [`domain/metrics-snapshot.md`](domain/metrics-snapshot.md), [`config.md`](config.md)

**Bối cảnh:** Dashboard cần xếp hạng theo tăng trưởng view thay vì lưu lịch sử score riêng.

**Quyết định:** Score dùng view growth và velocity; min/max được normalize trong từng lượt tính trên tập video của lượt đó, rồi chỉ lưu score hiện thời mỗi video.

**Lý do:** Score phản ánh tương quan của tập đang được tính, còn biến thiên lịch sử có thể suy từ snapshot.

**Trade-off:** Score giữa hai lượt tính không tự là thang đo tuyệt đối; không có lịch sử score để truy vấn trực tiếp.

**Hệ quả:** Không tuyên bố timestamp chung cho snapshot hoặc score cho đến khi Metrics Update được triển khai; công thức và config thuộc các tài liệu tham chiếu.

**Xem xét lại khi:** Cần so sánh score xuyên lượt hoặc lưu lịch sử score.

### DEC-012 — Scheduler dùng hosting built-in

**Status:** Đã chốt — SyncRun đã triển khai; Metrics/Cleanup chưa triển khai

**References:** [`architecture.md`](architecture.md), [`domain/background-jobs.md`](domain/background-jobs.md), [`../src/YTTrending.Infrastructure/Jobs/Sync/SyncChannelJob.cs`](../src/YTTrending.Infrastructure/Jobs/Sync/SyncChannelJob.cs)

**Bối cảnh:** Phase 1 cần lịch chạy đơn giản và use case vẫn phải gọi được từ HTTP hoặc worker.

**Quyết định:** Scheduler dùng `BackgroundService` + `PeriodicTimer`; nó tạo scope mỗi tick và gửi command. Không đưa Hangfire hoặc Quartz vào phạm vi hiện tại.

**Lý do:** Chưa có yêu cầu durable scheduler, retry dashboard hay distributed scheduling; logic giữ ở Application thay vì hosted service.

**Trade-off:** Không có cơ chế scheduler bền vững hay observability chuyên dụng.

**Hệ quả:** Scheduler Sync hiện chờ hết interval trước tick đầu. `MetricsUpdateEnabled` chỉ là config hiện hữu, chưa có worker/handler tương ứng trong source.

**Xem xét lại khi:** Cần retry bền vững, lịch phức tạp, dashboard job hoặc chạy nhiều instance.

### DEC-013 — Discovery lấy uploads playlist và dùng duration làm proxy Shorts

**Status:** Đã chốt

**References:** [`domain/discovery-engine.md`](domain/discovery-engine.md), [`config.md`](config.md), [`../src/YTTrending.Infrastructure/YouTube/YoutubeClient.cs`](../src/YTTrending.Infrastructure/YouTube/YoutubeClient.cs)

**Bối cảnh:** Cần lấy video mới với quota thấp mà vẫn giữ rule discovery ở Application.

**Quyết định:** Discovery đọc uploads playlist và chi tiết video, không dùng `search.list`. Với video thường, duration dương không vượt `ShortsMaxDurationSeconds` là proxy duy nhất để coi là Shorts; client cũng loại livestream/premiere, duration 0 và duration không parse được. Lọc theo ngày, view và quota candidate vẫn ở Application.

**Lý do:** Giảm chi phí truy vấn và không đẩy rule discovery vào client.

**Trade-off:** Đây không phải nhận diện Shorts chính thức của YouTube: có thể nhận nhầm video ngắn hoặc bỏ sót Shorts vượt ngưỡng; không dùng aspect ratio, hashtag hay endpoint phân loại riêng.

**Hệ quả:** Ngưỡng duration là cấu hình phân loại ở client; rule tracking chi tiết chỉ thuộc [`domain/discovery-engine.md`](domain/discovery-engine.md).

**Xem xét lại khi:** Có tín hiệu phân loại chính thức đáng tin cậy hoặc yêu cầu chính xác hơn được xác nhận.

### DEC-014 — SyncRun bất đồng bộ, recovery không resume

**Status:** Đã chốt

**References:** [`domain/background-jobs.md`](domain/background-jobs.md), [`api-contract.md`](api-contract.md), [`../src/YTTrending.Application/Features/Jobs/Commands/CreateSyncRun/CreateSyncRunCommandHandler.cs`](../src/YTTrending.Application/Features/Jobs/Commands/CreateSyncRun/CreateSyncRunCommandHandler.cs), [`../src/YTTrending.Application/Features/Jobs/Commands/ProcessSyncRun/ProcessSyncRunCommandHandler.cs`](../src/YTTrending.Application/Features/Jobs/Commands/ProcessSyncRun/ProcessSyncRunCommandHandler.cs), [`../src/YTTrending.Infrastructure/Jobs/Sync/SyncRunWorker.cs`](../src/YTTrending.Infrastructure/Jobs/Sync/SyncRunWorker.cs)

**Bối cảnh:** Sync-all không nên giữ HTTP request mở trong khi tuần tự xử lý nhiều channel.

**Quyết định:** Tạo `SyncRun` và snapshot item trong DB, sau đó enqueue `runId` cho worker; FE poll trạng thái. Khi khởi động, worker gửi recovery command để chuyển run chưa hoàn tất thành `Interrupted` và item chưa hoàn tất thành `Skipped`; không tự resume.

**Lý do:** Trạng thái run/item cần tồn tại sau request, còn retry mù sau crash có thể xử lý lại một channel đã save nhưng item chưa được đánh dấu thành công.

**Trade-off:** Save DB và enqueue in-memory không atomic. Process crash hoặc enqueue lỗi sau khi save có thể để run ở `Pending`; ở lần worker khởi động sau, nếu recovery save thành công, run đó sẽ thành `Interrupted`, không chạy lại.

**Hệ quả:** Worker xử lý tuần tự và lưu progress từng mốc. Lỗi nghiệp vụ hoặc lỗi YouTube của một item được map/saved rồi loop tiếp nếu các lần save thành công; không có cô lập DbContext theo channel vì worker tạo một scope/DbContext cho cả run. Lỗi DbContext hoặc `SaveChangesAsync` có thể làm processor thất bại; handler chỉ cố chốt item còn lại `Skipped`, và việc đó cũng cần một lần save thành công.

**Xem xét lại khi:** Cần delivery bền vững, resume/retry có idempotency, hoặc cô lập DbContext/transaction theo channel.

### DEC-015 — Phạm vi timestamp của SyncRun

**Status:** Đã chốt

**References:** [`database.md`](database.md), [`../src/YTTrending.Application/Features/Jobs/Commands/CreateSyncRun/CreateSyncRunCommandHandler.cs`](../src/YTTrending.Application/Features/Jobs/Commands/CreateSyncRun/CreateSyncRunCommandHandler.cs), [`../src/YTTrending.Application/Features/Jobs/Commands/SyncChannel/SyncChannelCommandHandler.cs`](../src/YTTrending.Application/Features/Jobs/Commands/SyncChannel/SyncChannelCommandHandler.cs)

**Bối cảnh:** Time fields của run, item và channel có ý nghĩa nghiệp vụ khác nhau.

**Quyết định:** `CreateSyncRun` lấy một mốc UTC cho `SyncRun.CreatedAt`. Mỗi `SyncChannelCommand` lấy một mốc UTC riêng, dùng nhất quán cho cooldown, cửa sổ discovery/lifecycle và `LastSyncAt`; item/run processing lấy mốc khi trạng thái của chính nó chuyển.

**Lý do:** Giữ rule của một channel nhất quán mà không gán thời điểm xử lý của channel khác vào nó.

**Trade-off:** Timestamp của các channel/item trong cùng run không đồng nhất và không phải thời điểm chính xác từng response YouTube được nhận.

**Hệ quả:** `SyncRunItem` không có created timestamp; không được diễn giải `SyncRun.CreatedAt` là thời điểm dữ liệu YouTube của toàn run được đọc. Bảo đảm batch cho snapshot/score chưa tồn tại vì Metrics Update chưa triển khai.

**Xem xét lại khi:** Cần timestamp đo thực tế theo request, hoặc cần một batch timestamp chung cho Metrics Update.

### DEC-016 — Lock và queue chỉ có hiệu lực trong một process

**Status:** Đã chốt

**References:** [`../src/YTTrending.Infrastructure/Concurrency/ChannelSyncLock.cs`](../src/YTTrending.Infrastructure/Concurrency/ChannelSyncLock.cs), [`../src/YTTrending.Infrastructure/Concurrency/SyncRunCreationLock.cs`](../src/YTTrending.Infrastructure/Concurrency/SyncRunCreationLock.cs), [`../src/YTTrending.Infrastructure/Jobs/Sync/SyncRunQueue.cs`](../src/YTTrending.Infrastructure/Jobs/Sync/SyncRunQueue.cs)

**Bối cảnh:** Phase 1 chạy một instance và gọi API ngoài trong lúc sync.

**Quyết định:** Dùng lock in-memory theo channel, lock tạo SyncRun và queue in-memory; không giữ DB lock/transaction trong thời gian gọi YouTube.

**Lý do:** Tránh giữ connection và lock database theo độ trễ mạng ngoài trong mô hình một process.

**Trade-off:** Không có mutual exclusion, delivery hay recovery xuyên process/instance.

**Hệ quả:** Chạy nhiều instance có thể tạo run trùng hoặc sync cùng channel; DB hiện không là coordinator phân tán.

**Xem xét lại khi:** Triển khai nhiều instance, cần queue bền vững hoặc cần distributed lock.

## Cần xác nhận

### DEC-017 — CORS theo môi trường

**Status:** Cần xác nhận — code và mô tả kiến trúc không cùng mức bảo đảm

**References:** [`../src/YTTrending.API/Program.cs`](../src/YTTrending.API/Program.cs), [`api-contract.md`](api-contract.md)

**Bối cảnh:** Angular ở origin khác cần CORS, nhưng `Program.cs` chỉ gọi `UseCors` trong Development.

**Quyết định:** Chưa có quyết định được xác nhận cho CORS ngoài Development; tài liệu không được khẳng định FE cross-origin hoạt động ở mọi môi trường.

**Lý do:** Code hiện có policy đọc origin từ config nhưng không áp policy ngoài nhánh Development.

**Trade-off:** Giữ trạng thái chưa quyết khiến deployment production cross-origin không có bảo đảm.

**Hệ quả:** Cần xác nhận deployment cùng origin hay cần bật policy ở môi trường khác trước khi cập nhật claim API/architecture.

**Xem xét lại khi:** Chốt topology triển khai frontend và API.

### DEC-018 — Chiến lược test và SQLite in-memory

**Status:** Cần xác nhận — chưa có test project trong workspace

**References:** [`architecture.md`](architecture.md), [`coding-convention.md`](coding-convention.md), [`../Directory.Packages.props`](../Directory.Packages.props)

**Bối cảnh:** Tài liệu có hướng dùng SQLite in-memory khi mở test, nhưng source hiện không có test project hay package test.

**Quyết định:** Chưa ghi nhận quyết định triển khai test hiện tại. Nếu dùng SQLite in-memory cho test nhanh, nó không thay PostgreSQL: SQL translation, migration, type semantics, constraint và concurrency có thể khác.

**Lý do:** Không có bằng chứng trong workspace để coi test strategy đã được thực thi hoặc coi SQLite là xác minh PostgreSQL.

**Trade-off:** Chưa có automated regression coverage; test SQLite sau này vẫn cần ranh giới rõ với integration test PostgreSQL.

**Hệ quả:** Không tuyên bố test đã tồn tại hoặc đã xác minh schema/runtime behavior.

**Xem xét lại khi:** Mở hạng mục test và chốt mức độ cần test với PostgreSQL thật.

### DEC-019 — Docker hóa API và promote artifact

**Status:** Đã chốt — implementation bắt đầu ở Batch 1 của kế hoạch deploy

**References:** [`../Dockerfile`](../Dockerfile), [`../src/YTTrending.API/YTTrending.API.csproj`](../src/YTTrending.API/YTTrending.API.csproj), [`../ai/plans/deploy-ghcr-vps.md`](../ai/plans/deploy-ghcr-vps.md)

**Bối cảnh:** Workspace có `Dockerfile` chưa được theo dõi và file này chưa khớp project API hiện tại. Source repo là public, còn production và deploy runner phải tách sang private deploy repo.

**Quyết định:** Sửa Dockerfile ở Batch 1 để build API và migrator Linux images; GitHub-hosted Actions ở public source repo publish hai image lên GHCR sau merge `master`. Production không deploy theo tag: operator lấy full source SHA cùng hai digest từ publish summary và manual-dispatch workflow trong private deploy repo. Private workflow mới pull `image@sha256:...`, có approval và chạy trên self-hosted runner chỉ gắn private repo.

**Lý do:** Tách public build khỏi production credentials/runner, đồng thời digest giữ artifact bất biến, truy vết và rollback được. Không cần credential cross-repo hoặc auto-trigger production từ public source repo.

**Trade-off:** Mỗi production release cần một bước promote thủ công có validation; đổi lại giảm nguy cơ deploy nhầm mutable tag và cho phép kiểm tra migration/backup trước downtime.

**Hệ quả:** Không dùng `latest`, `master` hoặc tag `sha-...` làm deploy input. Batch 1 phải sửa artifact Docker; Batch 3 phải publish GHCR; Batch 7 phải nhận SHA/digest qua `workflow_dispatch`. Trước khi các batch này pass, Docker deployment chưa được coi là hoạt động.

**Xem xét lại khi:** Tần suất deploy đủ cao để cần bot tạo PR digest trong private deploy repo; bot vẫn không được tự merge hoặc tự deploy production.

### DEC-020 — Ước tính quota YouTube

**Status:** Cần xác nhận — ước tính cũ không còn là bằng chứng vận hành

**References:** [YouTube quota calculator](https://developers.google.com/youtube/v3/determine_quota_cost), [`../src/YTTrending.Infrastructure/YouTube/YoutubeClient.cs`](../src/YTTrending.Infrastructure/YouTube/YoutubeClient.cs), [`config.md`](config.md)

**Bối cảnh:** Code không dùng `search.list`, nhưng số trang uploads thay đổi theo channel; Metrics Update và `GetVideoStatsAsync` chưa được triển khai.

**Quyết định:** Không coi một tổng unit cố định hoặc kết luận “không cần tăng quota” là bảo đảm hiện hành. Trước khi mở rộng, tính theo cấu hình, số trang thực tế, retry và quota của Google Cloud project.

**Lý do:** Một lượt discovery có thể cần nhiều `playlistItems.list`/`videos.list`; quota mặc định và chi phí endpoint thuộc chính sách bên ngoài, không phải hằng số của code.

**Trade-off:** Chưa có capacity commitment định lượng cho số channel lớn.

**Hệ quả:** Quy mô, lịch chạy và Metrics Update phải được đo/kiểm tra quota thực tế trước khi dùng làm căn cứ vận hành.

**Xem xét lại khi:** Có số liệu usage của project, cấu hình mục tiêu và implementation Metrics Update.

### DEC-021 — Lỗi DB dừng SyncRun

**Status:** Đã chốt

**References:** [`../src/YTTrending.Infrastructure/Jobs/Sync/SyncRunWorker.cs`](../src/YTTrending.Infrastructure/Jobs/Sync/SyncRunWorker.cs), [`../src/YTTrending.Application/Features/Jobs/Commands/ProcessSyncRun/ProcessSyncRunCommandHandler.cs`](../src/YTTrending.Application/Features/Jobs/Commands/ProcessSyncRun/ProcessSyncRunCommandHandler.cs)

**Bối cảnh:** Trong một SyncRun, channel A có thể sync xong, channel B gặp lỗi database, còn channel C chưa chạy.

**Quyết định:** Sau lỗi DB ở channel B, dừng SyncRun; không thử sync các channel C, D còn lại và đánh dấu item chưa hoàn tất là `Skipped`.

**Lý do:** Một `DbContext` vừa lỗi khi save có thể không còn dùng an toàn cho item sau; hiện không có context mới cho từng channel.

**Trade-off:** Các channel còn lại phải chờ một SyncRun mới; đổi lại không tái sử dụng context có thể đã lỗi.

**Hệ quả:** Handler hiện đã cố chuyển run sang `Failed` và item chưa hoàn tất sang `Skipped`. Việc chốt này vẫn phụ thuộc lần `SaveChangesAsync` cuối thành công; nếu DB không lưu được, recovery ở lần worker khởi động sau sẽ thử chốt run còn dang dở.

**Xem xét lại khi:** Cần tiếp tục các channel còn lại sau lỗi DB hoặc cần retry bền vững.
