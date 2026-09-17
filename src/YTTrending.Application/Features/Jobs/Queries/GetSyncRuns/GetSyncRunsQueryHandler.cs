using Microsoft.Extensions.Options;
using YTTrending.Application.Common.Options;
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRuns;
public sealed class GetSyncRunsQueryHandler(
    ISyncRunRepository syncRuns,
    IOptionsMonitor<SyncRunHistoryOptions> historyOptions,
    TimeProvider clock)
    : IRequestHandler<GetSyncRunsQuery, Result<PagedResult<SyncRunDto>>>
{
    public async Task<Result<PagedResult<SyncRunDto>>> Handle(GetSyncRunsQuery query, CancellationToken ct)
    {

        var now = clock.GetUtcNow();
        var lookbackDays = query.TimeRangeInDays
            ?? historyOptions.CurrentValue.DefaultLookbackDays;

        var filter = query with
        {
            From = query.From ?? now.AddDays(-lookbackDays),
            To = query.To ?? now
        };

        var result = await syncRuns.GetPagedAsync(filter, ct);
        var dtos = result.Items.Select(run => run.ToDto()).ToList();

        return Result<PagedResult<SyncRunDto>>.Success(
            new PagedResult<SyncRunDto>(
                dtos,
                result.Page,
                result.PageSize,
                result.TotalCount));
    }

}
