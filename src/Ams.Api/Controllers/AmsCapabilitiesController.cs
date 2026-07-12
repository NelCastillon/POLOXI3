using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Enterprise;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/enterprise/ams-capabilities")]
public sealed class AmsCapabilitiesController : ControllerBase
{
    private readonly IAmsCapabilityService _service;

    public AmsCapabilitiesController(IAmsCapabilityService service) => _service = service;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => await _service.GetByIdAsync(id, ct) is { } item ? Ok(item) : NotFound();

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid tenantId,
        [FromQuery] string? domainCode,
        [FromQuery] string? statusCode,
        [FromQuery] string? priorityCode,
        [FromQuery] string? searchTerm,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
        => Ok(await _service.SearchAsync(new SearchAmsCapabilitiesRequest(tenantId, domainCode, statusCode, priorityCode, searchTerm, activeOnly), ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAmsCapabilityRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }
}
