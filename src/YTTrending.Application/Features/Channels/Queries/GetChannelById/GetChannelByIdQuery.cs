using YTTrending.Application.Features.Channels.Dtos;

namespace YTTrending.Application.Features.Channels.Queries.GetChannelById;
public record GetChannelByIdQuery(int Id) : IRequest<Result<ChannelDto>>;

