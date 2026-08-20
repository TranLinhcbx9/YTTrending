
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.Channels.Dtos;

namespace YTTrending.Application.Features.Channels.Commands.UpdateChannel;
public class UpdateChannelCommandHandler(IChannelRepository channels, IUnitOfWork uow) : IRequestHandler<UpdateChannelCommand, Result<ChannelDto>>
{
    public async Task<Result<ChannelDto>> Handle(UpdateChannelCommand cmd, CancellationToken ct)
    {
        var channel = await channels.GetByIdAsync(cmd.Id, ct);
        if (channel is null)
            return Result<ChannelDto>.Failure(Error.NotFound("channel.notFound", "Không tìm channel với id được cung cấp."));

        channel.Name = cmd.Name;          // EF tự phát hiện thay đổi
        channel.Url = cmd.Url;
        channel.IsEnabled = cmd.IsEnabled;
        await uow.SaveChangesAsync(ct);    // UPDATE + audit UpdatedAt

        return Result<ChannelDto>.Success(channel.ToDto());
    }
}
