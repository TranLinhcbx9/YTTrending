
using YTTrending.Application.Common.Models.Filter;
using YTTrending.Application.Common.Models.Pagination;

namespace YTTrending.Application.Common.Interfaces.Persistence;
public interface ISyncRunRepository : IRepository<SyncRun>
{
    // Pending/Running đều active
    Task<bool> HasActiveAsync(CancellationToken ct);
    // Bản đọc no-tracking cho API summary, không dùng để cập nhật progress.
    Task<SyncRun?> GetByIdReadOnlyAsync(int id, CancellationToken ct);
    Task<PagedResult<SyncRunItem>> GetItemsPagedAsync(SyncRunItemFilter filter, CancellationToken ct);
    void CreateItems(IEnumerable<SyncRunItem> items);
}
