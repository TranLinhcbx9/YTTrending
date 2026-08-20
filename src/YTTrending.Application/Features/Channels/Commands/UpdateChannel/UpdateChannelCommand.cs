using YTTrending.Application.Features.Channels.Dtos;

namespace YTTrending.Application.Features.Channels.Commands.UpdateChannel;
public record UpdateChannelCommand(int Id, string Name, string Url, bool IsEnabled) : IRequest<Result<ChannelDto>>;
