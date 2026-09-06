namespace YTTrending.Application.Features.Jobs.SyncChannel;

/// <summary>
/// Facts của một lượt sync. FE sở hữu wording/toast và suy ra nội dung từ các count này.
/// </summary>
public sealed record SyncChannelResultDto(
    int FetchedShortsCount,
    int QualifiedShortsCount,
    int NewlyTrackedCount,
    int ExistingVideosRefreshedCount,
    int ArchivedVideosCount);
