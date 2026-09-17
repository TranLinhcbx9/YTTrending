
using YTTrending.Application.Common.Models.Filter;
using YTTrending.Application.Common.Models.Filters;

namespace YTTrending.Application.Common.Interfaces.Persistence;
public interface ISyncRunRepository : IRepository<SyncRun>
{
    Task<PagedResult<SyncRun>> GetPagedAsync(SyncRunFilter filter, CancellationToken ct);
    // Pending/Running đều active
    Task<bool> HasActiveAsync(CancellationToken ct);
    // Bản đọc no-tracking cho API summary, không dùng để cập nhật progress.
    Task<SyncRun?> GetByIdReadOnlyAsync(int id, CancellationToken ct);
    Task<PagedResult<SyncRunItem>> GetItemsPagedAsync(SyncRunItemFilter filter, CancellationToken ct);
    void CreateItems(IEnumerable<SyncRunItem> items);
    Task<List<SyncRunItem>> GetPendingItemsAsync(int syncRunId, CancellationToken ct);
    Task<List<SyncRun>> GetIncompleteAsync(CancellationToken ct);
    Task<List<SyncRunItem>> GetIncompleteItemsAsync(IReadOnlyList<int> syncRunIds, CancellationToken ct);
}
