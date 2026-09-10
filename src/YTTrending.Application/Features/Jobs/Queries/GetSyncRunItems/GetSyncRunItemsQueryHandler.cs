
using YTTrending.Application.Common.Interfaces.Persistence;
using YTTrending.Application.Common.Models.Pagination;
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRunItems;
public sealed class GetSyncRunItemsQueryHandler(ISyncRunRepository syncRuns)
    : IRequestHandler<GetSyncRunItemsQuery, Result<PagedResult<SyncRunItemDto>>>
{
    public async Task<Result<PagedResult<SyncRunItemDto>>> Handle(GetSyncRunItemsQuery request, CancellationToken cancellationToken)
    {
        var syncRun = await syncRuns.GetByIdAsync(request.SyncRunId, cancellationToken);
        if(syncRun is null)
        {
            return Result<PagedResult<SyncRunItemDto>>.Failure(Error.NotFound(SyncRunErrors.NotFound, "No sync run found."));
        }

        var result = await syncRuns.GetItemsPagedAsync(request, cancellationToken);
        var items = result.Items.Select(r => r.ToDto()).ToList();
        return Result<PagedResult<SyncRunItemDto>>.Success(new PagedResult<SyncRunItemDto>(items, result.Page, result.PageSize, result.TotalCount));
    }
}
