namespace YTTrending.Application.Features.Jobs.SyncChannel;
public sealed record SyncChannelCommand(int ChannelId) : IRequest<Result>
{
}
