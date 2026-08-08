namespace YTTrending.Domain.Entities;

/// <summary>
/// Bookmark video để tham khảo ý tưởng. 1 video chỉ bookmark được 1 lần
/// (unique index trên <see cref="VideoId"/>, cấu hình ở Infrastructure).
/// </summary>
public class SavedIdea : AuditableEntity
{
    public int Id { get; set; }

    public required int VideoId { get; set; }

    public string? Note { get; set; }
}
