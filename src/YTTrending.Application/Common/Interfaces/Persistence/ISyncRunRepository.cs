
namespace YTTrending.Application.Common.Interfaces.Persistence;
public interface ISyncRunRepository : IRepository<SyncRun>
{
    // Pending/Running đều active
    Task<bool> HasActiveAsync(CancellationToken ct);
    // Bản đọc no-tracking cho API summary, không dùng để cập nhật progress.
    Task<SyncRun?> GetByIdReadOnlyAsync(int id, CancellationToken ct);
    void CreateItems(IEnumerable<SyncRunItem> items);
}
