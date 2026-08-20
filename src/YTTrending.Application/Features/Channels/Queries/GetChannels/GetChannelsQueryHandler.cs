using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.Channels.Dtos;
namespace YTTrending.Application.Features.Channels.Queries.GetChannels;

public sealed class GetChannelsQueryHandler(IChannelRepository channels)
    : IRequestHandler<GetChannelsQuery, Result<PagedResult<ChannelDto>>>
{
    public async Task<Result<PagedResult<ChannelDto>>> Handle(GetChannelsQuery q, CancellationToken ct)
    {
        var (items, total) = await channels.GetPagedAsync(q.Page, q.PageSize, ct);
        var dtos = items.Select(c => c.ToDto()).ToList();
        return Result<PagedResult<ChannelDto>>.Success(new PagedResult<ChannelDto>(dtos, q.Page, q.PageSize, total));
    }
}
