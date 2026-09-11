using YTTrending.Application.Features.Jobs.Commands.CreateSyncRun;
using YTTrending.Application.Features.Jobs.Queries.GetSyncRun;
using YTTrending.Application.Features.Jobs.Queries.GetSyncRunItems;
using YTTrending.Application.Features.Jobs.Queries.GetSyncRuns;

namespace YTTrending.API.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetSyncRunsQuery query, CancellationToken ct)
    => (await sender.Send(query, ct)).ToActionResult();

    [HttpPost("sync")]
    public async Task<IActionResult> CreateSyncRun(CancellationToken ct)
    {
        var result = await sender.Send(new CreateSyncRunCommand(SyncRunTriggerType.Manual), ct);
        return result.IsSuccess
            ? Accepted(result.Value)
            : result.ToActionResult();
    }

    // Lấy thông tin chi tiết của một lần đồng bộ (sync run) theo ID.
    [HttpGet("sync/{id:int}")]
    public async Task<IActionResult> GetSyncRun(int id, CancellationToken ct)
        => (await sender.Send(new GetSyncRunQuery(id), ct)).ToActionResult();

    // Lấy danh sách các item/video thuộc một lần đồng bộ theo ID sync run.
    [HttpGet("sync/{id:int}/items")]
    public async Task<IActionResult> GetSyncRunItems(
        int id,
        [FromQuery] GetSyncRunItemsQuery query,
        CancellationToken ct)
        // `with` tạo query mới để SyncRunId bắt buộc lấy từ route.
        => (await sender.Send(query with { SyncRunId = id }, ct)).ToActionResult();
}
