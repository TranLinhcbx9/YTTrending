using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.Videos.Dtos;

namespace YTTrending.Application.Features.Videos.Queries.GetVideos;
public sealed class GetVideosQueryHandler(IVideoRepository videos) : IRequestHandler<GetVideosQuery, Result<PagedResult<VideoDto>>>
{
    public async Task<Result<PagedResult<VideoDto>>> Handle(GetVideosQuery request, CancellationToken cancellationToken)
    {
        var result = await videos.GetPagedAsync(request, cancellationToken);
        var dtos = result.Items.Select(v => v.ToDto()).ToList();
        return Result<PagedResult<VideoDto>>.Success(
            new PagedResult<VideoDto>(dtos, result.Page, result.PageSize, result.TotalCount));
    }
}
