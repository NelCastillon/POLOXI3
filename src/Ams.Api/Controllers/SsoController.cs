using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/iam/sso")]
public sealed class SsoController : ControllerBase
{
    private readonly ISsoConfigurationService _ssoService;
    private readonly IMfaDeviceService _mfaService;

    public SsoController(ISsoConfigurationService ssoService, IMfaDeviceService mfaService)
    {
        _ssoService = ssoService;
        _mfaService = mfaService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSsoById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _ssoService.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> SearchSso([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _ssoService.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("mfa/{id:guid}")]
    public async Task<IActionResult> GetMfaById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _mfaService.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("mfa")]
    public async Task<IActionResult> SearchMfa([FromQuery] Guid tenantId, [FromQuery] Guid? userId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _mfaService.SearchAsync(tenantId, userId, searchTerm, pageNumber, pageSize, cancellationToken));
}
