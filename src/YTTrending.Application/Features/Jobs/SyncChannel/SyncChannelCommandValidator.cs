
namespace YTTrending.Application.Features.Jobs.SyncChannel;
public sealed class SyncChannelCommandValidator : AbstractValidator<SyncChannelCommand>
{
    public SyncChannelCommandValidator()
    {
        RuleFor(x => x.ChannelId)
            .GreaterThan(0)
            .WithMessage("ChannelId phải lớn hơn 0.");
    }
}
