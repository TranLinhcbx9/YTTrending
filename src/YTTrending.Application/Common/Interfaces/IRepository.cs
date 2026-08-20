namespace YTTrending.Application.Common.Interfaces;

/// <summary>CRUD tối thiểu. KHÔNG thêm IQueryable/Find (→ generic-repo anti-pattern), KHÔNG Update (sửa = track+SaveChanges).</summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct);
    void Create(T entity);
    void Delete(T entity);
}
