using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Agency;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/agency")]
public sealed class AgencyController : ControllerBase
{
    private readonly IAgencyProfileService _service;
    public AgencyController(IAgencyProfileService service) => _service = service;

    [HttpGet("{tenantId:guid}")]
    public async Task<IActionResult> GetProfile(Guid tenantId, CancellationToken cancellationToken)
    {
        var profile = await _service.GetByTenantIdAsync(tenantId, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPut("{tenantId:guid}")]
    public async Task<IActionResult> UpdateProfile(Guid tenantId, [FromBody] UpdateAgencyProfileRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(tenantId, request, cancellationToken);
        return NoContent();
    }
}
