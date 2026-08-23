
using YTTrending.Application.Common.Interfaces;

namespace YTTrending.Application.Features.Channels.Commands.DeleteChannel;
public sealed class DeleteChannelCommandHandler (IChannelRepository channels, IUnitOfWork uow) : IRequestHandler<DeleteChannelCommand, Result>
{
    public async Task<Result> Handle(DeleteChannelCommand cmd, CancellationToken ct)
    {
        var channel = await channels.GetByIdAsync(cmd.Id, ct);
        if (channel is null)
            return Result.Failure(Error.NotFound(ChannelErrors.NotFound, "Không tìm channel với id được cung cấp."));

        channels.Delete(channel);
        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
