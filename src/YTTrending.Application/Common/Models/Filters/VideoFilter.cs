using FluentValidation.Validators;
using YTTrending.Application.Common.Models.Pagination;

namespace YTTrending.Application.Common.Models.Filters;

public record VideoFilter : PagedQuery
{
    public int[]? ChannelIds { get; init; }
    public VideoStatus? Status { get; init; }
    public long? MinViews { get; init; }
    public long? TimeRanges { get; init; }
}
