// DeleteChannelCommand.cs
using YTTrending.Application.Common.Models.Results;

namespace YTTrending.Application.Features.Channels.Commands.DeleteChannel;
public record DeleteChannelCommand(int Id) : IRequest<Result>;
