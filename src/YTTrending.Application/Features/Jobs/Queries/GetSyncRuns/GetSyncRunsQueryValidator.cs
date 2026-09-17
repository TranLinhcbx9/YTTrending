namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRuns;

public sealed class GetSyncRunsQueryValidator : AbstractValidator<GetSyncRunsQuery>
{
    public GetSyncRunsQueryValidator()
    {
        RuleFor(x => x.TimeRangeInDays)
            .GreaterThan(0)
            .When(x => x.TimeRangeInDays.HasValue);

        RuleFor(x => x)
            .Must(x => !x.TimeRangeInDays.HasValue
                || (!x.From.HasValue && !x.To.HasValue))
            .WithMessage("TimeRangeInDays cannot be combined with From or To.");

        RuleFor(x => x.To)
            .NotNull()
            .When(x => x.From.HasValue)
            .WithMessage("To must be provided when From is specified.");

        RuleFor(x => x.From)
            .NotNull()
            .When(x => x.To.HasValue)
            .WithMessage("From must be provided when To is specified.");

        RuleFor(x => x.To)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("To must be later than or equal to From.");
    }
}
