using YTTrending.Application.Common.Interfaces.Concurrency;
using YTTrending.Application.Common.Interfaces.Jobs;
using YTTrending.Application.Common.Interfaces.Persistence;
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Commands.CreateSyncRun;

public sealed class CreateSyncRunCommandHandler(
    ISyncRunRepository syncRuns,
    IChannelRepository channels,
    IUnitOfWork uow,
    ISyncRunQueue queue,
    ISyncRunCreationLock creationLock,
    TimeProvider clock)
    : IRequestHandler<CreateSyncRunCommand, Result<SyncRunDto>>
{
    public async Task<Result<SyncRunDto>> Handle(CreateSyncRunCommand command, CancellationToken ct)
    {
        // Khóa chỉ bảo vệ lúc tạo run; worker sẽ xử lý ở batch sau.
        if (!creationLock.TryAcquire())
        {
            return Result<SyncRunDto>.Failure(Error.Conflict(
                SyncRunErrors.InProgress,
                "Một lượt sync toàn bộ đang được tạo hoặc xử lý."));
        }

        try
        {
            if (await syncRuns.HasActiveAsync(ct))
            {
                return Result<SyncRunDto>.Failure(Error.Conflict(
                    SyncRunErrors.InProgress,
                    "Một lượt sync toàn bộ đang được xử lý."));
            }

            // Đây là snapshot target: bật/tắt channel sau dòng này không đổi item đã tạo.
            var enabledChannels = await channels.GetEnabledAsync(ct);
            if (enabledChannels.Count == 0)
            {
                return Result<SyncRunDto>.Failure(Error.Conflict(
                    SyncRunErrors.NoEnabledChannels,
                    "Không có channel nào đang bật để sync."));
            }

            // Một mốc UTC chung cho run và tất cả item của batch.
            var now = clock.GetUtcNow();
            var run = new SyncRun
            {
                TriggerType = command.TriggerType,
                Status = SyncRunStatus.Pending,
                TotalCount = enabledChannels.Count,
                CreatedAt = now,
            };
            // Navigation giúp EF tự gán SyncRunId sau khi insert run.
            var items = enabledChannels.Select(channel => new SyncRunItem
            {
                ChannelId = channel.Id,
                ChannelName = channel.Name,
                SyncRun = run,
            }).ToList();

            syncRuns.Create(run);
            syncRuns.CreateItems(items);
            await uow.SaveChangesAsync(ct);

            // Save thành công mới enqueue để worker không đọc một run chưa tồn tại.
            queue.Enqueue(run.Id);
            return Result<SyncRunDto>.Success(run.ToDto());
        }
        finally
        {
            creationLock.Release();
        }
    }
}
