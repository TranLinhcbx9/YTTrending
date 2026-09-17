
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRun;
public sealed record GetSyncRunQuery(int Id) : IRequest<Result<SyncRunDto>>
{
}
