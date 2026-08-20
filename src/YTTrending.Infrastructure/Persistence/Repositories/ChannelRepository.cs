namespace YTTrending.Infrastructure.Persistence.Repositories;

public sealed class ChannelRepository(YTTrendingDbContext db)
    : Repository<Channel>(db), IChannelRepository
{
    public Task<bool> ExistsByYoutubeIdAsync(string youtubeChannelId, CancellationToken ct) =>
        Set.AnyAsync(c => c.YoutubeChannelId == youtubeChannelId, ct);

    public async Task<(IReadOnlyList<Channel> Items, int Total)> GetPagedAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var query = Set.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Id);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}
