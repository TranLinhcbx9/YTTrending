using YTTrending.Application.Common.Extensions;
using YTTrending.Application.Common.Interfaces.Persistence;
using YTTrending.Application.Common.Models.Filters;
using YTTrending.Application.Common.Models.Pagination;
using YTTrending.Infrastructure.Persistence;

namespace YTTrending.Infrastructure.Persistence.Repositories;

public sealed class ChannelRepository(YTTrendingDbContext db)
    : Repository<Channel>(db), IChannelRepository
{
    public Task<bool> ExistsByYoutubeIdAsync(string youtubeChannelId, CancellationToken ct) =>
        Set.AnyAsync(c => c.YoutubeChannelId == youtubeChannelId, ct);

    public Task<PagedResult<Channel>> GetPagedAsync(ChannelFilter filter, CancellationToken ct)
        => Set.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Id)
            .ToPagedResultAsync(filter.Page, filter.PageSize, ct);
    public Task<List<Channel>> GetEnabledAsync(CancellationToken ct)
        => Set.AsNoTracking()
            .OrderBy(x => x.Id)
            .Where(c => c.IsEnabled == true).ToListAsync(ct);

}
