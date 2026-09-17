namespace YTTrending.Domain.Entities;

public class Channel : AuditableEntity
{
    public int Id { get; set; }

    public required string YoutubeChannelId { get; set; }

    public required string Name { get; set; }

    public required string Url { get; set; }

    /// <summary>
    /// Playlist "uploads" của channel — bất biến theo channel, YouTube trả kèm lúc resolve handle
    /// nên lưu luôn để mỗi lượt sync khỏi gọi lại channels.list.
    /// <para>
    /// Nullable vì channel thêm trước thay đổi này chưa có giá trị — coi null là "chưa biết",
    /// hỏi YouTube đúng một lần rồi lưu lại.
    /// </para>
    /// </summary>
    public string? UploadsPlaylistId { get; set; }

    /// <summary>Channel mới add là bật tracking — mặc định của bool là false nên phải khai tường minh.</summary>
    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? LastSyncAt { get; set; }
}
