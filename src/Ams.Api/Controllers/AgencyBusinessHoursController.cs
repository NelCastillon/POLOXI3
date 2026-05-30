using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Agency;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/agency/business-hours")]
public sealed class AgencyBusinessHoursController : ControllerBase
{
    private readonly IAgencyBusinessHoursService _service;

    public AgencyBusinessHoursController(IAgencyBusinessHoursService service)
        => _service = service;

    [HttpGet("tenant/{tenantId:guid}")]
    public async Task<IActionResult> GetByTenant(Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetByTenantIdAsync(tenantId, cancellationToken));

    [HttpPut("tenant/{tenantId:guid}")]
    public async Task<IActionResult> Update(Guid tenantId, [FromBody] UpdateAgencyBusinessHoursRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(tenantId, request, cancellationToken);
        return NoContent();
    }
}
