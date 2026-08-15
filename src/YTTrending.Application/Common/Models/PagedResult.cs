namespace YTTrending.Application.Common.Models;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    private readonly int _pageSize = PageSize >= 1
        ? PageSize
        : throw new ArgumentOutOfRangeException(nameof(PageSize), PageSize, "PageSize phải ≥ 1.");

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value >= 1
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "PageSize phải ≥ 1.");
    }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
}
