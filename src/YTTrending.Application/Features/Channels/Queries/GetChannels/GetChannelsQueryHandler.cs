using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.Channels.Dtos;
namespace YTTrending.Application.Features.Channels.Queries.GetChannels;

public sealed class GetChannelsQueryHandler(IChannelRepository channels)
    : IRequestHandler<GetChannelsQuery, Result<PagedResult<ChannelDto>>>
{
    public async Task<Result<PagedResult<ChannelDto>>> Handle(GetChannelsQuery q, CancellationToken ct)
    {
        var result = await channels.GetPagedAsync(q, ct);
        var dtos = result.Items.Select(c => c.ToDto()).ToList();
        return Result<PagedResult<ChannelDto>>.Success(
            new PagedResult<ChannelDto>(dtos, result.Page, result.PageSize, result.TotalCount));
    }
}
