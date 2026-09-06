namespace YTTrending.Application.Features.Videos.Dtos;

public record VideoDto(
    int Id, string YoutubeVideoUrl, int ChannelId, string ChannelName,
    string Title, DateTimeOffset PublishedAt, int DurationSeconds,
    string? ThumbnailUrl, VideoStatus Status,
    long LatestViews, long LatestLikes, long LatestComments);

public static class VideoMappings
{
    public static VideoDto ToDto(this Video v) => new(
        v.Id, $"https://www.youtube.com/shorts/{v.YoutubeVideoId}", v.ChannelId, v.Channel.Name,
        v.Title, v.PublishedAt, v.DurationSeconds,
        v.ThumbnailUrl, v.Status, v.LatestViews, v.LatestLikes, v.LatestComments);
}
