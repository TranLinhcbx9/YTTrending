// DeleteChannelCommand.cs
namespace YTTrending.Application.Features.Channels.Commands.DeleteChannel;
public record DeleteChannelCommand(int Id) : IRequest<Result>;
