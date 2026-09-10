
using Microsoft.Extensions.Logging;
using YTTrending.Application.Features.Channels;
using YTTrending.Application.Features.Jobs.Commands.SyncChannel;

namespace YTTrending.Application.Features.Jobs.Commands.ProcessSyncRun;
public sealed class ProcessSyncRunCommandHandler(
    ISyncRunRepository syncRuns,
    IUnitOfWork uow,
    ISender sender,
    TimeProvider clock,
    ILogger<ProcessSyncRunCommandHandler> logger)
    : IRequestHandler<ProcessSyncRunCommand, Result>
{
    public async Task<Result> Handle(ProcessSyncRunCommand command, CancellationToken ct)
    {
        // Tracked entity để processor cập nhật durable progress trên đúng run này.
        var run = await syncRuns.GetByIdAsync(command.SyncRunId, ct);
        // Queue có thể trùng id hoặc run đã recovery; bỏ qua để không chạy lại.
        if (run is null || run.Status != SyncRunStatus.Pending)
            return Result.Success();

        try
        {
            // Save mốc Running ngay để restart biết run đã bắt đầu.
            run.Status = SyncRunStatus.Running;
            run.StartedAt = clock.GetUtcNow();
            await uow.SaveChangesAsync(ct);

            var items = await syncRuns.GetPendingItemsAsync(run.Id, ct);
            foreach (var item in items)
            {
                // Save trước call YouTube để recovery nhận diện item đang dở dang.
                item.Status = SyncRunItemStatus.Running;
                item.StartedAt = clock.GetUtcNow();
                await uow.SaveChangesAsync(ct);

                try
                {
                    var result = await sender.Send(new SyncChannelCommand(item.ChannelId, run.TriggerType), ct);
                    CompleteItem(run, item, result.Error);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Sync run {SyncRunId} failed for channel {ChannelId}", run.Id, item.ChannelId);
                    item.Status = SyncRunItemStatus.Failed;
                    item.ErrorCode = SyncRunErrors.ChannelExecutionFailed;
                    item.ErrorMessage = "Không thể đồng bộ kênh.";
                    run.FailedCount++;
                }

                // Item terminal + counter được save từng vòng, không gom đến cuối run.
                item.CompletedAt = clock.GetUtcNow();
                await uow.SaveChangesAsync(ct);
            }

            run.Status = run.FailedCount > 0 || run.SkippedCount > 0
                ? SyncRunStatus.CompletedWithIssues
                : SyncRunStatus.Completed;
            run.CompletedAt = clock.GetUtcNow();
            await uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Sync run {SyncRunId} could not be completed.", run.Id);

            // Processor lỗi: mọi item chưa terminal đều được chốt Skipped để progress không bị kẹt.
            var unfinishedItems = await syncRuns.GetIncompleteItemsAsync([run.Id], ct);
            var now = clock.GetUtcNow();
            foreach (var item in unfinishedItems)
            {
                item.Status = SyncRunItemStatus.Skipped;
                item.ErrorCode = SyncRunErrors.ExecutionFailed;
                item.ErrorMessage = "Không thể hoàn tất lượt sync.";
                item.CompletedAt = now;
            }

            run.Status = SyncRunStatus.Failed;
            run.SkippedCount += unfinishedItems.Count;
            run.ErrorCode = SyncRunErrors.ExecutionFailed;
            run.ErrorMessage = "Sync run could not be completed.";
            run.CompletedAt = now;
            await uow.SaveChangesAsync(ct);
            throw;
        }
    }

    private static void CompleteItem(SyncRun run, SyncRunItem item, Error? error)
    {
        if (error is null)
        {
            item.Status = SyncRunItemStatus.Succeeded;
            run.SuccessCount++;
            return;
        }

        // Lỗi nghiệp vụ dự kiến bỏ qua item; exception hạ tầng là Failed.
        if (error.Code is ChannelErrors.SyncInProgress or ChannelErrors.SyncTooSoon or ChannelErrors.NotFound)
        {
            item.Status = SyncRunItemStatus.Skipped;
            run.SkippedCount++;
        }
        else
        {
            item.Status = SyncRunItemStatus.Failed;
            run.FailedCount++;
        }

        item.ErrorCode = error.Code;
        item.ErrorMessage = error.Message;
    }
}
