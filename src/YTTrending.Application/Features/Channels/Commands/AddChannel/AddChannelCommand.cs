

using YTTrending.Application.Features.Channels.Dtos;
namespace YTTrending.Application.Features.Channels.Commands.AddChannel;
public sealed record AddChannelCommand(string YoutubeChannelId) : IRequest<Result<ChannelDto>>;
