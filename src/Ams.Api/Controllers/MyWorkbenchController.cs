using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Workbench;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/workbench")]
public sealed class MyWorkbenchController : ControllerBase
{
    private readonly IMyWorkbenchService _service;

    public MyWorkbenchController(IMyWorkbenchService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? viewCode = null,
        [FromQuery] string? priorityCode = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] DateOnly? workDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAsync(new MyWorkbenchRequest
        {
            TenantId = tenantId,
            UserId = userId,
            SearchTerm = searchTerm,
            ViewCode = viewCode,
            PriorityCode = priorityCode,
            StatusCode = statusCode,
            WorkDate = workDate
        }, cancellationToken);

        return Ok(result);
    }

    [HttpPatch("tasks/{taskItemId:guid}/status")]
    public async Task<IActionResult> SetTaskStatus(Guid taskItemId, [FromBody] MyWorkbenchTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        await _service.SetTaskStatusAsync(taskItemId, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> SetNotificationRead(Guid notificationId, [FromBody] MyWorkbenchNotificationStatusRequest request, CancellationToken cancellationToken = default)
    {
        await _service.SetNotificationReadAsync(notificationId, request, cancellationToken);
        return NoContent();
    }
}
