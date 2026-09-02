
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.Channels.Dtos;

namespace YTTrending.Application.Features.Channels.Commands.AddChannel;
public sealed class AddChannelCommandHandler(
    IUnitOfWork uow,
    IChannelRepository channels,
    IYouTubeClient youtube)
    : IRequestHandler<AddChannelCommand, Result<ChannelDto>>
{
    public async Task<Result<ChannelDto>> Handle(AddChannelCommand cmd, CancellationToken ct)
    {
        // Input là handle (@name) — phải resolve qua YouTube trước mới có VideoId/ChannelId chuẩn để so trùng
        var info = await youtube.GetChannelAsync(cmd.YoutubeHandle, ct);
        if (info is null)
            return Result<ChannelDto>.Failure(Error.NotFound(ChannelErrors.NotFound, "Không tìm thấy channel trên YouTube."));

        if (await channels.ExistsByYoutubeIdAsync(info.YoutubeChannelId, ct))
            return Result<ChannelDto>.Failure(Error.Conflict(ChannelErrors.AlreadyExists, "Channel này đã được theo dõi."));

        var channel = new Channel
        {
            YoutubeChannelId = info.YoutubeChannelId,
            Name = info.Name,
            Url = info.Url,
            UploadsPlaylistId = info.UploadsPlaylistId,
        };
        channels.Create(channel);
        await uow.SaveChangesAsync(ct);

        return Result<ChannelDto>.Success(channel.ToDto());
    }
}
