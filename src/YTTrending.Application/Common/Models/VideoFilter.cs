namespace YTTrending.Application.Common.Models;

public record VideoFilter : PagedQuery
{
    public int[]? ChannelIds { get; init; }
    public VideoStatus? Status { get; init; }
}
