using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Communications;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MessagesController : ControllerBase
{
    private readonly IMessageService _service;
    public MessagesController(IMessageService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetThreads([FromQuery] Guid tenantId, [FromQuery] string? channel,
        [FromQuery] string? status, [FromQuery] string? assignedTo, [FromQuery] string? searchTerm,
        CancellationToken cancellationToken)
        => Ok(await _service.GetThreadsAsync(
               new GetThreadsRequest(tenantId, channel, status, assignedTo, searchTerm), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var thread = await _service.GetThreadByIdAsync(id, cancellationToken);
        return thread is null ? NotFound() : Ok(thread);
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest request, CancellationToken cancellationToken)
        => Ok(await _service.SendMessageAsync(request, cancellationToken));

    [HttpPost("{id:guid}/reply")]
    public async Task<IActionResult> Reply(Guid id, [FromBody] ReplyMessageRequest request, CancellationToken cancellationToken)
        => Ok(await _service.ReplyAsync(request with { ThreadId = id }, cancellationToken));

    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignThreadRequest request, CancellationToken cancellationToken)
    {
        await _service.AssignAsync(request with { ThreadId = id }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid id, [FromBody] EscalateThreadRequest request, CancellationToken cancellationToken)
    {
        await _service.EscalateAsync(request with { ThreadId = id }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, CancellationToken cancellationToken)
    {
        await _service.ResolveAsync(new ResolveThreadRequest(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        await _service.MarkReadAsync(new MarkReadRequest(id), cancellationToken);
        return NoContent();
    }
}
