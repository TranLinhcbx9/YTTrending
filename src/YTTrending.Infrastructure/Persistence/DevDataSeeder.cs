namespace YTTrending.Infrastructure.Persistence;

/// <summary>Seed channel giả cho Development — idempotent.</summary>
public static class DevDataSeeder
{
    public static async Task SeedAsync(IChannelRepository channels, IUnitOfWork uow, CancellationToken ct = default)
    {
        var (_, total) = await channels.GetPagedAsync(1, 1, ct);
        if (total > 0) return;

        for (var i = 1; i <= 4; i++)
        {
            var id = $"UCseed{i:D3}";
            channels.Create(new Channel
            {
                YoutubeChannelId = id,
                Name = $"Seed Channel {i}",
                Url = $"https://www.youtube.com/channel/{id}",
            });
        }
        await uow.SaveChangesAsync(ct);
    }
}
