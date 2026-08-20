using Ams.Application.Abstractions.Services;
using Ams.Application.Features.PolicyCoverages;
using Ams.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/policies/coverages")]
public sealed class PolicyCoveragesController : ControllerBase
{
    private readonly IPolicyCoverageService _service;

    public PolicyCoveragesController(IPolicyCoverageService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetByPolicy([FromQuery] Guid tenantId, [FromQuery] Guid policyId, CancellationToken cancellationToken)
        => AuthenticatedRequestContext.CanViewPolicy(User, tenantId)
            ? Ok(await _service.GetByPolicyAsync(tenantId, policyId, cancellationToken))
            : Forbid();

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => AuthenticatedRequestContext.CanViewPolicy(User, tenantId)
            ? Ok(await _service.GetTemplatesAsync(tenantId, cancellationToken))
            : Forbid();

    [HttpGet("by-code")]
    public async Task<IActionResult> GetByCode([FromQuery] Guid tenantId, [FromQuery] Guid policyId, [FromQuery] string coverageCode, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanViewPolicy(User, tenantId)) return Forbid();
        var item = await _service.GetByCodeAsync(tenantId, policyId, coverageCode, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePolicyCoverageDetailRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, request.TenantId)) return Forbid();
        request.CreatedByUserId = AuthenticatedRequestContext.GetUserId(User);
        return Ok(new { id = await _service.CreateAsync(request, cancellationToken) });
    }

    [HttpGet("{coverageDetailId:guid}")]
    public async Task<IActionResult> GetById(Guid coverageDetailId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanViewPolicy(User, tenantId)) return Forbid();
        var item = await _service.GetByIdAsync(tenantId, coverageDetailId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("{coverageDetailId:guid}")]
    public async Task<IActionResult> Update(Guid coverageDetailId, [FromBody] UpdatePolicyCoverageDetailRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, request.TenantId)) return Forbid();
        request.ModifiedByUserId = AuthenticatedRequestContext.GetUserId(User);
        await _service.UpdateAsync(coverageDetailId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{coverageDetailId:guid}")]
    public async Task<IActionResult> Delete(Guid coverageDetailId, [FromQuery] Guid tenantId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, tenantId)) return Forbid();
        await _service.DeleteAsync(coverageDetailId, new DeletePolicyCoverageDetailRequest { TenantId = tenantId, ModifiedByUserId = AuthenticatedRequestContext.GetUserId(User) }, cancellationToken);
        return NoContent();
    }

    [HttpPost("fields")]
    public async Task<IActionResult> CreateField([FromBody] CreatePolicyCoverageFieldRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, request.TenantId)) return Forbid();
        request.CreatedByUserId = AuthenticatedRequestContext.GetUserId(User);
        return Ok(new { id = await _service.CreateFieldAsync(request, cancellationToken) });
    }

    [HttpPut("fields/{fieldId:guid}")]
    public async Task<IActionResult> UpdateField(Guid fieldId, [FromBody] UpdatePolicyCoverageFieldRequest request, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, request.TenantId)) return Forbid();
        request.ModifiedByUserId = AuthenticatedRequestContext.GetUserId(User);
        await _service.UpdateFieldAsync(fieldId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("fields/{fieldId:guid}")]
    public async Task<IActionResult> DeleteField(Guid fieldId, [FromQuery] Guid tenantId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        if (!AuthenticatedRequestContext.CanManagePolicy(User, tenantId)) return Forbid();
        await _service.DeleteFieldAsync(tenantId, fieldId, AuthenticatedRequestContext.GetUserId(User), cancellationToken);
        return NoContent();
    }
}
