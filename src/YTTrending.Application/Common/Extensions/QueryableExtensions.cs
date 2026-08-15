using System.Linq.Expressions;
using YTTrending.Application.Common.Models;

namespace YTTrending.Application.Common.Extensions;

public static class QueryableExtensions
{
    /// <summary>Phân trang offset. Query <b>phải</b> đã có OrderBy với khóa duy nhất.</summary>
    /// <remarks>
    /// ⚠️ Thiếu OrderBy — hoặc OrderBy theo cột có ties (Score, LatestViews) mà không kèm
    /// tiebreaker duy nhất — thì Postgres không cam kết thứ tự giữa 2 lần query: trang 2
    /// lặp/mất item. Luôn kết bằng <c>.ThenBy(x => x.Id)</c>.
    /// </remarks>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new PagedResult<T>(items, page, pageSize, total);
    }

    /// <summary>Chỉ áp <paramref name="predicate"/> khi <paramref name="condition"/> đúng — bỏ được rừng if lồng nhau khi filter là optional.</summary>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> q, bool condition, Expression<Func<T, bool>> predicate)
        => condition ? q.Where(predicate) : q;
}
