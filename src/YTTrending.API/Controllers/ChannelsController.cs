using YTTrending.Application.Features.Channels.Commands.AddChannel;
using YTTrending.Application.Features.Channels.Commands.DeleteChannel;
using YTTrending.Application.Features.Channels.Commands.UpdateChannel;
using YTTrending.Application.Features.Channels.Queries.GetChannelById;
using YTTrending.Application.Features.Channels.Queries.GetChannels;

namespace YTTrending.API.Controllers;

[ApiController]
[Route("api/channels")]
public sealed class ChannelsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AddChannelCommand command, CancellationToken ct)
        => (await sender.Send(command, ct)).ToActionResult();

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetChannelsQuery query, CancellationToken ct)
        => (await sender.Send(query, ct)).ToActionResult();

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
        => (await sender.Send(new GetChannelByIdQuery(id), ct)).ToActionResult();

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Edit(int id, [FromBody] UpdateChannelCommand command, CancellationToken ct)
        => (await sender.Send(command with { Id = id }, ct)).ToActionResult();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => (await sender.Send(new DeleteChannelCommand(id), ct)).ToActionResult();
}
