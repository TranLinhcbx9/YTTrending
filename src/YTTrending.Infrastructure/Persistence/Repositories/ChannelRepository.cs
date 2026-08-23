using YTTrending.Application.Common.Extensions;
using YTTrending.Application.Common.Models;

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
}
