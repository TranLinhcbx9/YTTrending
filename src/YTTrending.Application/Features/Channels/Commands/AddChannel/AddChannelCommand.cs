

using YTTrending.Application.Features.Channels.Dtos;
namespace YTTrending.Application.Features.Channels.Commands.AddChannel;
public sealed record AddChannelCommand(string YoutubeHandle) : IRequest<Result<ChannelDto>>;
