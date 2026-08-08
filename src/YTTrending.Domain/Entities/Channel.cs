namespace YTTrending.Domain.Entities;

public class Channel : AuditableEntity
{
    public int Id { get; set; }

    public required string YoutubeChannelId { get; set; }

    public required string Name { get; set; }

    public required string Url { get; set; }

    /// <summary>Channel mới add là bật tracking — mặc định của bool là false nên phải khai tường minh.</summary>
    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? LastSyncAt { get; set; }
}
