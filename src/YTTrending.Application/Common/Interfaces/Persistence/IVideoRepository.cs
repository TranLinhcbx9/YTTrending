using YTTrending.Application.Common.Models.Filters;
using YTTrending.Application.Common.Models.Pagination;

namespace YTTrending.Application.Common.Interfaces.Persistence;
public interface IVideoRepository : IRepository<Video>
{
    Task<PagedResult<Video>> GetPagedAsync(VideoFilter filter, CancellationToken ct);
    Task<Video?> GetByIdWithChannelAsync(int id, CancellationToken ct);
    Task<List<Video>> GetActiveByChannelIdAsync(int channelId, CancellationToken ct);
}
