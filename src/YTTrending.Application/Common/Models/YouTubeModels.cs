namespace YTTrending.Application.Common.Models;

/// <summary>Khớp đúng 3 field required của entity Channel — handler gán thẳng, không phải bịa.</summary>
public record ChannelInfo(string YoutubeChannelId, string Name, string Url);

/// <summary>
/// Đủ để insert một row `videos` từ một lần gọi: 4 field required lấy được từ YouTube
/// (ChannelId là int nội bộ, handler tự biết) + 2 field nullable + 3 metrics.
/// <para>
/// Views có mặt ở đây chính là thứ Discovery dùng để lọc MinViewsThreshold TRƯỚC khi quyết
/// định lưu, và cũng là số seed thẳng vào 3 cột denormalize latest_* (docs/decisions.md mục
/// Domain — mục 2).
/// </para>
/// </summary>
public record ShortVideoInfo(
    string YoutubeVideoId,
    string Title,
    DateTimeOffset PublishedAt,
    int DurationSeconds,
    string? Description,
    string? ThumbnailUrl,
    long Views,
    long Likes,
    long Comments);

/// <summary>
/// Metrics Update Job chỉ cần 3 số, không kéo lại metadata.
/// <para>
/// KHÔNG mang mốc thời gian: YouTube không trả "đo lúc nào", nên SnapshotAt do job set bằng
/// TimeProvider — một mốc `now` duy nhất cho cả lượt sync, để mọi snapshot cùng lần thẳng
/// hàng và chart View Growth không răng cưa.
/// </para>
/// </summary>
public record VideoStats(string YoutubeVideoId, long Views, long Likes, long Comments);
