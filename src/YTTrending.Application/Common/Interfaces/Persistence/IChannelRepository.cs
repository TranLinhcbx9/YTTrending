using YTTrending.Application.Common.Models.Filters;
using YTTrending.Application.Common.Models.Pagination;

namespace YTTrending.Application.Common.Interfaces.Persistence;
public interface IChannelRepository : IRepository<Channel>
{
    Task<bool> ExistsByYoutubeIdAsync(string youtubeChannelId, CancellationToken ct);
    Task<PagedResult<Channel>> GetPagedAsync(ChannelFilter filter, CancellationToken ct);
}
