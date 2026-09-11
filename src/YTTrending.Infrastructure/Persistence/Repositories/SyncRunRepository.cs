using YTTrending.Application.Common.Extensions;
using YTTrending.Application.Common.Interfaces.Persistence;
using YTTrending.Application.Common.Models.Filter;
using YTTrending.Application.Common.Models.Filters;
using YTTrending.Application.Common.Models.Pagination;

namespace YTTrending.Infrastructure.Persistence.Repositories;

public sealed class SyncRunRepository(YTTrendingDbContext db, TimeProvider timeProvider)
    : Repository<SyncRun>(db), ISyncRunRepository
{
    public Task<PagedResult<SyncRun>> GetPagedAsync(SyncRunFilter filter, CancellationToken ct) =>
        Set.AsNoTracking()
            .WhereIf(filter.Status.HasValue, s => s.Status == filter.Status!.Value)
            .WhereIf(filter.Source.HasValue, s => s.TriggerType == filter.Source!.Value)
            .WhereIf(filter.From.HasValue && filter.To.HasValue,
                s => (s.StartedAt ?? s.CreatedAt) >= filter.From!.Value
                    && (s.StartedAt ?? s.CreatedAt) <= filter.To!.Value)
            .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);

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

    public Task<List<SyncRunItem>> GetPendingItemsAsync(int syncRunId, CancellationToken ct)
        => db.SyncRunItems
            .Where(s => s.SyncRunId == syncRunId && s.Status == SyncRunItemStatus.Pending)
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

    public Task<List<SyncRun>> GetIncompleteAsync(CancellationToken ct)
        => Set.Where(s => s.Status == SyncRunStatus.Pending || s.Status == SyncRunStatus.Running)
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

    public Task<List<SyncRunItem>> GetIncompleteItemsAsync(IReadOnlyList<int> syncRunIds, CancellationToken ct) =>
        db.SyncRunItems.Where(x => syncRunIds.Contains(x.SyncRunId)
                && (x.Status == SyncRunItemStatus.Pending || x.Status == SyncRunItemStatus.Running))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

    public void CreateItems(IEnumerable<SyncRunItem> items) => db.SyncRunItems.AddRange(items);
}
