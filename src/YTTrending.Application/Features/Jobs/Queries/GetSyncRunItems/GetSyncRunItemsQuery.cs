
using YTTrending.Application.Common.Models.Filter;
using YTTrending.Application.Common.Models.Pagination;
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRunItems;
public sealed record GetSyncRunItemsQuery : SyncRunItemFilter, IRequest<Result<PagedResult<SyncRunItemDto>>>
{
}
