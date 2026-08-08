namespace YTTrending.Domain.Common;

/// <summary>
/// Chỉ <c>Channel</c> và <c>Video</c> kế thừa — hai bảng duy nhất có cặp cột
/// <c>created_at</c> / <c>updated_at</c>. Các entity còn lại mang thời gian
/// nghiệp vụ (<c>snapshot_at</c>, <c>calculated_at</c>, <c>archived_at</c>)
/// và phải set tường minh ở chỗ tạo record.
/// </summary>
/// <remarks>
/// Kiểu này đồng thời là bộ lọc của <c>ChangeTracker.Entries&lt;AuditableEntity&gt;()</c>
/// trong <c>AppDbContext</c> — kế thừa thêm entity nào vào đây là entity đó bị
/// điền audit tự động.
/// </remarks>
public abstract class AuditableEntity
{
    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
