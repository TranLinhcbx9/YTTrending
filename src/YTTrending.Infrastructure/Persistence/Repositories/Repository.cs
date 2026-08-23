

namespace YTTrending.Infrastructure.Persistence.Repositories;
public class Repository<T>(YTTrendingDbContext db) : IRepository<T> where T : class
{
    protected DbSet<T> Set { get; } = db.Set<T>();

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct) =>
        await Set.FindAsync(new object?[] { id }, ct);

    public void Create(T entity) => Set.Add(entity);
    public void Delete(T entity) => Set.Remove(entity);
}
