using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Common.Interfaces.Persistence;
using YTTrending.Domain.Enums;

namespace YTTrending.Infrastructure.Persistence.Repositories;

public sealed class SyncRunRepository(YTTrendingDbContext db)
    : Repository<SyncRun>(db), ISyncRunRepository
{
    public Task<bool> HasActiveAsync(CancellationToken ct) =>
        Set.AnyAsync(x => x.Status == SyncRunStatus.Pending || x.Status == SyncRunStatus.Running, ct);

    public Task<SyncRun?> GetByIdReadOnlyAsync(int id, CancellationToken ct) =>
        Set.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    public void CreateItems(IEnumerable<SyncRunItem> items) => db.SyncRunItems.AddRange(items);
}
