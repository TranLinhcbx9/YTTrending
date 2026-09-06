namespace YTTrending.Application.Common.Models;

public sealed record ShortsDiscoveryResult(
    int FetchedShortsCount,
    int QualifiedShortsCount,
    IReadOnlyList<ShortVideoInfo> SelectedShorts);
