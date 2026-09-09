using YTTrending.Application.Features.Jobs.Commands.CreateSyncRun;
using YTTrending.Application.Features.Jobs.Queries.GetSyncRun;
using YTTrending.Domain.Enums;

namespace YTTrending.API.Controllers;

[ApiController]
[Route("api/jobs")]
public sealed class JobsController(ISender sender) : ControllerBase
{
    [HttpPost("sync")]
    public async Task<IActionResult> CreateSyncRun(CancellationToken ct)
    {
        var result = await sender.Send(new CreateSyncRunCommand(SyncRunTriggerType.Manual), ct);
        return result.IsSuccess
            ? Accepted(result.Value)
            : result.ToActionResult();
    }

    [HttpGet("sync/{id:int}")]
    public async Task<IActionResult> GetSyncRun(int id, CancellationToken ct)
        => (await sender.Send(new GetSyncRunQuery(id), ct)).ToActionResult();
}
