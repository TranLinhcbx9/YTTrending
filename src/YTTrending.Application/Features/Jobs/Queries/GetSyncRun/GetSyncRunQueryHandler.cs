using YTTrending.Application.Common.Interfaces.Persistence;
using YTTrending.Application.Features.Jobs.Dtos;

namespace YTTrending.Application.Features.Jobs.Queries.GetSyncRun;

public sealed class GetSyncRunQueryHandler(ISyncRunRepository syncRuns)
    : IRequestHandler<GetSyncRunQuery, Result<SyncRunDto>>
{
    public async Task<Result<SyncRunDto>> Handle(GetSyncRunQuery query, CancellationToken ct)
    {
        var run = await syncRuns.GetByIdReadOnlyAsync(query.Id, ct);
        return run is null
            ? Result<SyncRunDto>.Failure(Error.NotFound(SyncRunErrors.NotFound, "Không tìm thấy lượt sync."))
            : Result<SyncRunDto>.Success(run.ToDto());
    }
}
