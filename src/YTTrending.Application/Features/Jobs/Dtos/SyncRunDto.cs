namespace YTTrending.Application.Features.Jobs.Dtos;

public sealed record SyncRunDto(
    int Id,
    SyncRunTriggerType TriggerType,
    SyncRunStatus Status,
    int TotalCount,
    int SuccessCount,
    int SkippedCount,
    int FailedCount,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt)
{
    // Không lưu DB vì luôn suy ra được từ các counter item đã terminal.
    public int ProcessedCount => SuccessCount + SkippedCount + FailedCount;
}

public static class SyncRunMappings
{
    // API trả DTO, không trả EF entity trực tiếp cho FE.
    public static SyncRunDto ToDto(this SyncRun run) => new(
        run.Id, run.TriggerType, run.Status, run.TotalCount, run.SuccessCount,
        run.SkippedCount, run.FailedCount, run.ErrorCode, run.ErrorMessage,
        run.CreatedAt, run.StartedAt, run.CompletedAt);
}
