using YTTrending.Application.Features.Jobs.Dtos;
namespace YTTrending.Application.Features.Jobs.Commands.SyncChannel;

public sealed record SyncChannelCommand(
    int ChannelId,
    SyncRunTriggerType TriggerType = SyncRunTriggerType.Manual)
    : IRequest<Result<SyncChannelResultDto>>;
