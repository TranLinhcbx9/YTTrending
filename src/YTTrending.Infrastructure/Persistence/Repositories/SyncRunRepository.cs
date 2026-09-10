using YTTrending.Application.Common.Extensions;
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Common.Interfaces.Persistence;
using YTTrending.Application.Common.Models.Filter;
using YTTrending.Application.Common.Models.Pagination;

namespace YTTrending.Infrastructure.Persistence.Repositories;

public sealed class SyncRunRepository(YTTrendingDbContext db)
    : Repository<SyncRun>(db), ISyncRunRepository
{
    public Task<bool> HasActiveAsync(CancellationToken ct) =>
        Set.AnyAsync(x => x.Status == SyncRunStatus.Pending || x.Status == SyncRunStatus.Running, ct);

    public Task<SyncRun?> GetByIdReadOnlyAsync(int id, CancellationToken ct) =>
        Set.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PagedResult<SyncRunItem>> GetItemsPagedAsync(SyncRunItemFilter filter, CancellationToken ct)
    {
        var query = db.SyncRunItems
            .AsNoTracking()
            .Where(s => s.SyncRunId == filter.SyncRunId)
            .WhereIf(filter.Status.HasValue, s => s.Status == filter.Status!.Value)
            .OrderBy(s => s.Id)
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);
        return query;
    }


    public void CreateItems(IEnumerable<SyncRunItem> items) => db.SyncRunItems.AddRange(items);
}
