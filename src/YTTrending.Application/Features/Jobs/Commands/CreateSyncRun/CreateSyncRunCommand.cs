using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Commands.CreateSyncRun;

public sealed record CreateSyncRunCommand(SyncRunTriggerType TriggerType)
    : IRequest<Result<SyncRunDto>>;
