using Microsoft.AspNetCore.Mvc;
using YTTrending.Application.Features.Videos.Queries.GetVideoById;
using YTTrending.Application.Features.Videos.Queries.GetVideos;

namespace YTTrending.API.Controllers;
[ApiController]
[Route("api/videos")]
public sealed class VideosController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetVideosQuery query, CancellationToken ct)
    {
        var result = await sender.Send(query, ct);
        return result.ToActionResult();
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await sender.Send(new GetVideoByIdQuery(id), ct);
        return result.ToActionResult();
    }
}
