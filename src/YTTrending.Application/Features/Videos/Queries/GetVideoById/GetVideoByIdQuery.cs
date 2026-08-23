using YTTrending.Application.Features.Videos.Dtos;

namespace YTTrending.Application.Features.Videos.Queries.GetVideoById;
public record GetVideoByIdQuery(int Id) : IRequest<Result<VideoDto>>;
