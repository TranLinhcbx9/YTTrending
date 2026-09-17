namespace YTTrending.Application.Features.Channels.Dtos;

public record ChannelDto(
    int Id,
    string YoutubeChannelId,
    string Name,
    string Url,
    string? UploadsPlaylistId,
    bool IsEnabled,
    DateTimeOffset? LastSyncAt,
    DateTimeOffset CreatedAt);

public static class ChannelMappings
{
    public static ChannelDto ToDto(this Channel c) =>
        new(c.Id, c.YoutubeChannelId, c.Name, c.Url, c.UploadsPlaylistId, c.IsEnabled, c.LastSyncAt, c.CreatedAt);
}
