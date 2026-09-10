namespace YTTrending.Application.Features.Jobs;

public static class SyncRunErrors
{
    public const string InProgress = "syncRun.inProgress";
    public const string NoEnabledChannels = "syncRun.noEnabledChannels";
    public const string NotFound = "syncRun.notFound";
    public const string Interrupted = "syncRun.interrupted";
    public const string ChannelExecutionFailed = "syncRun.channelExecutionFailed";
    public const string ExecutionFailed = "syncRun.executionFailed";
}
