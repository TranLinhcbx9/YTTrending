
namespace YTTrending.Application.Features.Channels.Commands.AddChannel;
public sealed class AddChannelCommandValidator : AbstractValidator<AddChannelCommand>
{
    public AddChannelCommandValidator()
    {
        RuleFor(x => x.YoutubeChannelId)
            .NotEmpty().WithMessage("YoutubeChannelId is required.");
    }
}
