using Ams.Application.Abstractions.Services;
using Ams.Application.Features.AiConfig;
using Ams.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/ai-config")]
public sealed class AiConfigController : ControllerBase
{
    private readonly IAiConfigService _service;
    public AiConfigController(IAiConfigService service) => _service = service;

    private Guid TenantId=>AuthenticatedRequestContext.GetTenantId(User)??throw new UnauthorizedAccessException("An authenticated tenant context is required.");

    [HttpGet("{id:guid}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) => (await _service.GetByIdAsync(TenantId,id, ct)) is { } item ? Ok(item) : NotFound();

    [HttpGet]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string kind, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _service.SearchAsync(TenantId, kind, searchTerm, pageNumber, pageSize, ct));

    [HttpPost]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Create([FromBody] CreateAiConfigItemRequest request, CancellationToken ct)
    {
        var id = await _service.CreateAsync(request with{TenantId=TenantId}, ct);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAiConfigItemRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(TenantId,id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(TenantId,id, ct);
        return NoContent();
    }
}
