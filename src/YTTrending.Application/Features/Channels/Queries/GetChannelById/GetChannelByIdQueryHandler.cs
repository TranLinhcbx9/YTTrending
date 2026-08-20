using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.Channels.Dtos;

namespace YTTrending.Application.Features.Channels.Queries.GetChannelById;
public sealed class GetChannelByIdQueryHandler(IChannelRepository channels) : IRequestHandler<GetChannelByIdQuery, Result<ChannelDto>>
{
    public async Task<Result<ChannelDto>> Handle(GetChannelByIdQuery q, CancellationToken ct)
    {
        var channel = await channels.GetByIdAsync(q.Id, ct);
        if (channel is null)
            return Result<ChannelDto>.Failure(Error.NotFound("channel.notFound", $"Channel with ID {q.Id} not found."));
        return Result<ChannelDto>.Success(channel.ToDto());
    }
}
