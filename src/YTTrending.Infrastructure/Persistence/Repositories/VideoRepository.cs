using YTTrending.Application.Common.Extensions;
using YTTrending.Application.Common.Models;

namespace YTTrending.Infrastructure.Persistence.Repositories;

public sealed class VideoRepository(YTTrendingDbContext db)
    : Repository<Video>(db), IVideoRepository
{
    public Task<PagedResult<Video>> GetPagedAsync(VideoFilter filter, CancellationToken ct)
        => Set.AsNoTracking()
            .Include(v => v.Channel)
            .WhereIf(filter.ChannelIds is { Length: > 0 }, v => filter.ChannelIds!.Contains(v.ChannelId))
            .WhereIf(filter.Status.HasValue, v => v.Status == filter.Status!.Value)
            .OrderByDescending(v => v.PublishedAt).ThenBy(v => v.Id)
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);

    public Task<Video?> GetByIdWithChannelAsync(int id, CancellationToken ct) =>
        Set.Include(v => v.Channel).FirstOrDefaultAsync(v => v.Id == id, ct);
}
