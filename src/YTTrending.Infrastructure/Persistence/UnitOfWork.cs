
namespace YTTrending.Infrastructure.Persistence;
public sealed class UnitOfWork(YTTrendingDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
