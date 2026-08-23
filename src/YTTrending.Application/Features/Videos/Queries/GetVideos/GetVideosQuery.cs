using YTTrending.Application.Features.Videos.Dtos;

namespace YTTrending.Application.Features.Videos.Queries.GetVideos;
public record GetVideosQuery : VideoFilter, IRequest<Result<PagedResult<VideoDto>>>;

