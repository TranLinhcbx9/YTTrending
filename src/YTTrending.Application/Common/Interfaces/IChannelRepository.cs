

namespace YTTrending.Application.Common.Interfaces;
public interface IChannelRepository : IRepository<Channel>
{
    Task<bool> ExistsByYoutubeIdAsync(string youtubeChannelId, CancellationToken ct);
    Task<(IReadOnlyList<Channel> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken ct);
}
