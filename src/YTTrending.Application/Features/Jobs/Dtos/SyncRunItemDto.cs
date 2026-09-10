 
namespace YTTrending.Application.Features.Jobs.Dtos;
public sealed record SyncRunItemDto(
        int Id,
        int SyncRunId,
        int ChannelId,
        string ChannelName,
        SyncRunItemStatus? Status,
        string? ErrorCode,
        string? ErrorMessage,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt);
public static class SyncRunItemMappings
{
    public static SyncRunItemDto ToDto(this SyncRunItem item) =>
        new SyncRunItemDto(
            item.Id,
            item.SyncRunId,
            item.ChannelId,
            item.ChannelName,
            item.Status,
            item.ErrorCode,
            item.ErrorMessage,
            item.StartedAt,
            item.CompletedAt);
}

