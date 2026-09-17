namespace YTTrending.Application.Common.Models.Sync;

public sealed record VideoSyncResult(
    int NewlyDiscoveredCount,
    int NewlyTrackedCount,
    int ExistingVideosRefreshedCount,
    int ArchivedVideosCount);
