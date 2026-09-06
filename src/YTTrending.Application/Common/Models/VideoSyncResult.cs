namespace YTTrending.Application.Common.Models;

public sealed record VideoSyncResult(
    int NewlyDiscoveredCount,
    int NewlyTrackedCount,
    int ExistingVideosRefreshedCount,
    int ArchivedVideosCount);
