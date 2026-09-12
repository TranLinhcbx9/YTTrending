using MediatR;
using Microsoft.Extensions.Hosting;
using YTTrending.Application.Features.Jobs;
using YTTrending.Application.Features.Jobs.Commands.CreateSyncRun;

namespace YTTrending.Infrastructure.Jobs.Sync;

public sealed class SyncChannelJob(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<JobOptions> jobOptions,
    IOptionsMonitor<TrackingOptions> trackingOptions,
    ILogger<SyncChannelJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Tạo timer lại sau mỗi tick để hot-reload interval có hiệu lực ở chu kỳ kế tiếp.
                using var timer = new PeriodicTimer(
                    TimeSpan.FromHours(trackingOptions.CurrentValue.SyncIntervalHours));
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;

                if (!jobOptions.CurrentValue.SyncEnabled)
                {
                    logger.LogInformation("Scheduled sync tick skipped because Jobs:SyncEnabled is false");
                    continue;
                }

                // Hosted service là singleton; scope cấp ISender/DbContext scoped cho tick này.
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var result = await sender.Send(
                    new CreateSyncRunCommand(SyncRunTriggerType.Scheduled), stoppingToken);

                if (!result.IsSuccess && result.Error?.Code == SyncRunErrors.InProgress)
                    logger.LogInformation("Scheduled sync tick skipped because a sync run is active");
                else if (!result.IsSuccess)
                    logger.LogWarning("Scheduled sync run was not created: {ErrorCode}", result.Error?.Code);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled sync tick failed");
            }
        }
    }
}
