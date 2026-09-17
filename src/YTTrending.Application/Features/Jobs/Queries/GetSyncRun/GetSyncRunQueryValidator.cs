namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRun;

public sealed class GetSyncRunQueryValidator : AbstractValidator<GetSyncRunQuery>
{
    public GetSyncRunQueryValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
