namespace YTTrending.Application.Common.Models;

public record VideoFilter : PagedQuery
{
    public int? ChannelId { get; init; }
    public VideoStatus? Status { get; init; }
}
