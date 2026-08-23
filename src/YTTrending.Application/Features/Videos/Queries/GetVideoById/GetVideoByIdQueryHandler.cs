
using YTTrending.Application.Common.Interfaces;
using YTTrending.Application.Features.Videos.Dtos;

namespace YTTrending.Application.Features.Videos.Queries.GetVideoById;
public sealed class GetVideoByIdQueryHandler(IVideoRepository videos) : IRequestHandler<GetVideoByIdQuery, Result<VideoDto>>
{
    public async Task<Result<VideoDto>> Handle(GetVideoByIdQuery q, CancellationToken ct)
    {
        var video = await videos.GetByIdWithChannelAsync(q.Id, ct);
        if (video is null)
            return Result<VideoDto>.Failure(Error.NotFound(VideoErrors.NotFound, $"Video with ID {q.Id} not found."));
        return Result<VideoDto>.Success(video.ToDto());
    }
}
