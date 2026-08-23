

namespace YTTrending.Application.Common.Interfaces;
public interface IChannelRepository : IRepository<Channel>
{
    Task<bool> ExistsByYoutubeIdAsync(string youtubeChannelId, CancellationToken ct);
    Task<PagedResult<Channel>> GetPagedAsync(ChannelFilter filter, CancellationToken ct);
}
