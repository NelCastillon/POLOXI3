using Ams.Application.Abstractions.Services;
using Ams.Application.Features.PolicyCoverages;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/policies/coverages")]
public sealed class PolicyCoveragesController : ControllerBase
{
    private readonly IPolicyCoverageService _service;

    public PolicyCoveragesController(IPolicyCoverageService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetByPolicy([FromQuery] Guid tenantId, [FromQuery] Guid policyId, CancellationToken cancellationToken)
        => Ok(await _service.GetByPolicyAsync(tenantId, policyId, cancellationToken));

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetTemplatesAsync(tenantId, cancellationToken));

    [HttpGet("by-code")]
    public async Task<IActionResult> GetByCode([FromQuery] Guid tenantId, [FromQuery] Guid policyId, [FromQuery] string coverageCode, CancellationToken cancellationToken)
    {
        var item = await _service.GetByCodeAsync(tenantId, policyId, coverageCode, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePolicyCoverageDetailRequest request, CancellationToken cancellationToken)
        => Ok(new { id = await _service.CreateAsync(request, cancellationToken) });

    [HttpGet("{coverageDetailId:guid}")]
    public async Task<IActionResult> GetById(Guid coverageDetailId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(tenantId, coverageDetailId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("{coverageDetailId:guid}")]
    public async Task<IActionResult> Update(Guid coverageDetailId, [FromBody] UpdatePolicyCoverageDetailRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(coverageDetailId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{coverageDetailId:guid}")]
    public async Task<IActionResult> Delete(Guid coverageDetailId, [FromQuery] Guid tenantId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(coverageDetailId, new DeletePolicyCoverageDetailRequest { TenantId = tenantId, ModifiedByUserId = modifiedByUserId }, cancellationToken);
        return NoContent();
    }

    [HttpPost("fields")]
    public async Task<IActionResult> CreateField([FromBody] CreatePolicyCoverageFieldRequest request, CancellationToken cancellationToken)
        => Ok(new { id = await _service.CreateFieldAsync(request, cancellationToken) });

    [HttpPut("fields/{fieldId:guid}")]
    public async Task<IActionResult> UpdateField(Guid fieldId, [FromBody] UpdatePolicyCoverageFieldRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateFieldAsync(fieldId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("fields/{fieldId:guid}")]
    public async Task<IActionResult> DeleteField(Guid fieldId, [FromQuery] Guid tenantId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteFieldAsync(tenantId, fieldId, modifiedByUserId, cancellationToken);
        return NoContent();
    }
}
