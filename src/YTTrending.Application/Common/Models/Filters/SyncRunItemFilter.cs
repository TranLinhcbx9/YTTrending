using YTTrending.Application.Common.Models.Pagination;

namespace YTTrending.Application.Common.Models.Filter;

public record SyncRunItemFilter : PagedQuery
{
    public int SyncRunId { get; init; }
    public SyncRunItemStatus? Status { get; init; }
}
