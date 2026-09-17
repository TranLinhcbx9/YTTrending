using System.ComponentModel.DataAnnotations;

namespace YTTrending.Application.Common.Options;

public sealed class SyncRunHistoryOptions
{
    public const string SectionName = "SyncRunHistory";

    [Range(1, 365)]
    public int DefaultLookbackDays { get; init; }
}
