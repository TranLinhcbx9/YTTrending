
namespace YTTrending.Application.Features.Channels.Commands.AddChannel;
public sealed class AddChannelCommandValidator : AbstractValidator<AddChannelCommand>
{
    public AddChannelCommandValidator()
    {
        RuleFor(x => x.YoutubeHandle)
            .NotEmpty().WithMessage("YoutubeHandle is required.");
    }
}
