namespace YTTrending.Application.Features.Jobs.Commands.ProcessSyncRun;

public sealed record ProcessSyncRunCommand(int SyncRunId) : IRequest<Result>;
