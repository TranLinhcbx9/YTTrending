namespace YTTrending.Application.Common.Options;
public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    // Kill-switch cho toàn bộ background job — tắt ở Development để đỡ tốn quota YouTube Data API lúc debug (A5)
    public bool Enabled { get; init; }
}