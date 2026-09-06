namespace YTTrending.Application.Common.Models;

public sealed record VideoSyncResult(
    int NewlyTrackedCount,
    int ExistingVideosRefreshedCount,
    int ArchivedVideosCount);
