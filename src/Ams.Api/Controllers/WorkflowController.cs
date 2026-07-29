using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class WorkflowController : ControllerBase
{
    private readonly IWorkflowService _service;

    public WorkflowController(IWorkflowService service)
    {
        _service = service;
    }


    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate([FromBody] InitiateWorkflowRequest request, CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty || request.TargetEntityId == Guid.Empty)
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Workflow request is invalid", Detail = "Tenant and target entity identifiers are required." });

        try
        {
            var id = await _service.InitiateAsync(request.TenantId, request.TargetEntityName, request.TargetEntityId, request.WorkflowDefinitionId, request.UserId, request.Notes, cancellationToken);
            return Ok(new IdResult { Id = id });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Workflow could not be initiated", Detail = exception.Message });
        }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        await _service.ApproveAsync(id, request.UserId, request.Notes, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        await _service.RejectAsync(id, request.UserId, request.Reason ?? request.Notes, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/return")]
    public async Task<IActionResult> Return(Guid id, [FromBody] WorkflowActionRequest request, CancellationToken cancellationToken)
    {
        await _service.ReturnAsync(id, request.UserId, request.Reason ?? request.Notes, cancellationToken);
        return NoContent();
    }

    public sealed class InitiateWorkflowRequest
    {
        public Guid TenantId { get; set; }
        [Required, StringLength(100)]
        public string TargetEntityName { get; set; } = string.Empty;
        public Guid TargetEntityId { get; set; }
        public Guid? WorkflowDefinitionId { get; set; }
        public Guid? UserId { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class WorkflowActionRequest
    {
        public Guid? UserId { get; set; }
        public string? Notes { get; set; }
        public string? Reason { get; set; }
    }
}
