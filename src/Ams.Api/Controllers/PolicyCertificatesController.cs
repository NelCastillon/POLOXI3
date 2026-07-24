using Ams.Application.Abstractions.Services;
using Ams.Application.Features.PolicyCertificates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/policy-certificates")]
public sealed class PolicyCertificatesController : ControllerBase
{
    private readonly IPolicyCertificateService _service;

    public PolicyCertificatesController(IPolicyCertificateService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid tenantId,
        [FromQuery] string? searchTerm,
        [FromQuery] string? status,
        [FromQuery] string? certificateType,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
        => Ok(await _service.SearchAsync(tenantId, searchTerm, status, certificateType, pageNumber, pageSize, cancellationToken));

    [HttpGet("{certificateId:guid}")]
    public async Task<IActionResult> GetById(Guid certificateId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(tenantId, certificateId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("by-number/{certificateNumber}")]
    public async Task<IActionResult> GetByNumber([FromQuery] Guid tenantId, string certificateNumber, CancellationToken cancellationToken)
    {
        var item = await _service.GetByNumberAsync(tenantId, certificateNumber, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePolicyCertificateRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { certificateId = id }, new { id });
    }

    [HttpPut("{certificateId:guid}")]
    public async Task<IActionResult> Update(Guid certificateId, [FromBody] UpdatePolicyCertificateRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(certificateId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{certificateId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid certificateId, [FromBody] RevokePolicyCertificateRequest request, CancellationToken cancellationToken)
    {
        await _service.RevokeAsync(certificateId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{certificateId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid certificateId, [FromBody] RestorePolicyCertificateRequest request, CancellationToken cancellationToken)
    {
        await _service.RestoreAsync(certificateId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("{certificateId:guid}/deliver")]
    public async Task<IActionResult> MarkDelivered(Guid certificateId, [FromBody] PolicyCertificateActionRequest request, CancellationToken cancellationToken)
    {
        await _service.MarkDeliveredAsync(certificateId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{certificateId:guid}")]
    public async Task<IActionResult> Delete(Guid certificateId, [FromQuery] Guid tenantId, [FromQuery] Guid? modifiedByUserId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(certificateId, tenantId, modifiedByUserId, cancellationToken);
        return NoContent();
    }
}
