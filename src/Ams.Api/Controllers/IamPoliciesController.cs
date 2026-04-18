using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/iam/policies")]
public sealed class IamPoliciesController : ControllerBase
{
    private readonly IIamPolicyService _service;
    public IamPoliciesController(IIamPolicyService service) => _service = service;

    [HttpGet("fields/{id:guid}")]
    public async Task<IActionResult> GetFieldPolicyById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetFieldPolicyByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("fields")]
    public async Task<IActionResult> SearchFieldPolicies([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchFieldPoliciesAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("records/{id:guid}")]
    public async Task<IActionResult> GetRecordPolicyById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetRecordPolicyByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("records")]
    public async Task<IActionResult> SearchRecordPolicies([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchRecordPoliciesAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));
}
