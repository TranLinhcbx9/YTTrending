namespace YTTrending.Application.Common.Models.Filters;

public record class SyncRunFilter : PagedQuery
{
    public SyncRunStatus? Status { get; init; }
    public SyncRunTriggerType? Source { get; init; }
    public int? TimeRangeInDays { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}
