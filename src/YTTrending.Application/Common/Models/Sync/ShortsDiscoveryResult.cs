using YTTrending.Application.Common.Models.Youtube;

namespace YTTrending.Application.Common.Models.Sync;

public sealed record ShortsDiscoveryResult(
    int FetchedShortsCount,
    int QualifiedShortsCount,
    IReadOnlyList<ShortVideoInfo> SelectedShorts);
