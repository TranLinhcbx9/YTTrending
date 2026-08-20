using YTTrending.Application.Features.Channels.Dtos;

namespace YTTrending.Application.Features.Channels.Queries.GetChannels;

public record GetChannelsQuery : PagedQuery, IRequest<Result<PagedResult<ChannelDto>>>;
