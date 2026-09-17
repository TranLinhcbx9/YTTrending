namespace YTTrending.Domain.Entities;

/// <summary>
/// Bản ghi tiến độ và tổng kết bền vững cho một lượt sync toàn bộ channel.
/// </summary>
public class SyncRun
{
    public int Id { get; set; }

    // Phân biệt lượt chạy tay với lịch; trigger quyết định cooldown được áp dụng.
    public required SyncRunTriggerType TriggerType { get; set; }
    public SyncRunStatus Status { get; set; } = SyncRunStatus.Pending;

    // Snapshot số channel enabled lúc tạo run; thay đổi channel sau đó không đổi total này.
    public required int TotalCount { get; set; }

    // Tổng item đã đạt trạng thái cuối; ProcessedCount suy ra từ ba counter này.
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }

    // Chỉ dùng khi processor không thể hoàn tất cả run; kết quả riêng từng channel nằm ở SyncRunItem.
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    // Mốc thời gian nghiệp vụ do processor set, không phải audit field do EF tự duy trì.
    public required DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
