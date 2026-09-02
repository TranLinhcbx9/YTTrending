namespace YTTrending.Application.Common.Options;
public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    // Kill-switch riêng cho Sync Channel Job — tắt ở Development để đỡ tốn quota YouTube Data API lúc debug (A5)
    public bool SyncEnabled { get; init; }

    // Kill-switch riêng cho Metrics Update Job — tách khỏi SyncEnabled để bật/tắt độc lập
    public bool MetricsUpdateEnabled { get; init; }
}
