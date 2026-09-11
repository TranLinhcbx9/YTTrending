using YTTrending.Application.Common.Models.Filters;
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRuns;
public sealed record GetSyncRunsQuery : SyncRunFilter, IRequest<Result<PagedResult<SyncRunDto>>>
{
}
