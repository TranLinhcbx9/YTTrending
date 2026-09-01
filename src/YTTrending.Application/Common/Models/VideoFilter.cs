using FluentValidation.Validators;

namespace YTTrending.Application.Common.Models;

public record VideoFilter : PagedQuery
{
    public int[]? ChannelIds { get; init; }
    public VideoStatus? Status { get; init; }
    public long? MinViews { get; init; }
    public long? TimeRanges { get; init; }
}
