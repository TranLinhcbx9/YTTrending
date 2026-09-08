namespace YTTrending.Domain.Entities;

/// <summary>
/// Snapshot công việc và kết quả của một channel trong <see cref="SyncRun"/>.
/// </summary>
public class SyncRunItem
{
    public int Id { get; set; }

    // EF gán FK qua SyncRun khi lưu run và item cùng lúc.
    public int SyncRunId { get; set; }

    // Tham chiếu lịch sử, không FK tới Channel để xóa channel vẫn giữ được lịch sử run.
    public required int ChannelId { get; set; }

    // Snapshot tên hiển thị cho UI/report, kể cả khi channel bị đổi tên hoặc xóa.
    public required string ChannelName { get; set; }

    public SyncRunItemStatus Status { get; set; } = SyncRunItemStatus.Pending;

    // Lý do riêng của channel khi bị bỏ qua hoặc chạy lỗi.
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    // Mốc thời gian xử lý channel trong run cha.
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    // Navigation bắt buộc để EF tạo item cùng run cha trước khi run có database ID.
    public required SyncRun SyncRun { get; set; }
}
