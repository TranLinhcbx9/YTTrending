
namespace YTTrending.Application.Features.Jobs.Commands.InterruptIncompleteSyncRuns;

public sealed class InterruptIncompleteSyncRunsCommandHandler(
    ISyncRunRepository syncRuns,
    IUnitOfWork uow,
    TimeProvider clock)
    : IRequestHandler<InterruptIncompleteSyncRunsCommand, Result>
{
    public async Task<Result> Handle(InterruptIncompleteSyncRunsCommand command, CancellationToken ct)
    {
        var runs = await syncRuns.GetIncompleteAsync(ct);
        if (runs.Count == 0)
            return Result.Success();

        var runIds = runs.Select(x => x.Id).ToList();
        var items = await syncRuns.GetIncompleteItemsAsync(runIds, ct);
        var now = clock.GetUtcNow();

        // Không resume: crash có thể xảy ra sau channel save nhưng trước item được mark success.
        foreach (var item in items)
        {
            item.Status = SyncRunItemStatus.Skipped;
            item.ErrorCode = SyncRunErrors.Interrupted;
            item.ErrorMessage = "Ứng dụng dừng trước khi channel được xử lý xong.";
            item.CompletedAt = now;
        }

        foreach (var run in runs)
        {
            run.Status = SyncRunStatus.Interrupted;
            run.SkippedCount += items.Count(x => x.SyncRunId == run.Id);
            run.ErrorCode = SyncRunErrors.Interrupted;
            run.ErrorMessage = "Ứng dụng dừng trước khi lượt sync hoàn tất.";
            run.CompletedAt = now;
        }

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
