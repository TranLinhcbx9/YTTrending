
namespace YTTrending.Application.Common.Interfaces;
public interface IVideoRepository : IRepository<Video>
{
    Task<PagedResult<Video>> GetPagedAsync(VideoFilter filter, CancellationToken ct);
    Task<Video?> GetByIdWithChannelAsync(int id, CancellationToken ct);
}
