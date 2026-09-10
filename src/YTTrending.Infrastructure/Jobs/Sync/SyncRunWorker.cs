using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using YTTrending.Application.Common.Interfaces.Jobs;
using YTTrending.Application.Features.Jobs.Commands.InterruptIncompleteSyncRuns;
using YTTrending.Application.Features.Jobs.Commands.ProcessSyncRun;

namespace YTTrending.Infrastructure.Jobs.Sync;

public sealed class SyncRunWorker(
    IServiceScopeFactory scopeFactory,
    ISyncRunQueue queue,
    ILogger<SyncRunWorker> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken ct)
    {
        // Chốt run cũ trước khi worker nhận run mới; recovery không resume.
        using var scope = scopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new InterruptIncompleteSyncRunsCommand(), ct);
        await base.StartAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var syncRunId = await queue.DequeueAsync(stoppingToken);
                // Hosted service singleton nên mỗi run cần scope mới cho ISender/DbContext scoped.
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                await sender.Send(new ProcessSyncRunCommand(syncRunId), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Sync run worker failed while processing a queued run");
            }
        }
    }
}
