using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// Tenant Configuration control plane (/admin/configuration). Reads/writes DB-backed
// tenant-scoped configuration. Configuration never grants access — these endpoints
// only shape how already-permitted capabilities behave. Gated by the granular
// admin.configuration.* permissions seeded in migration 0248.
[ApiController]
[Route("api/tenant_configuration")]
[Authorize]
public sealed class TenantConfigurationController(ITenantConfigurationService service) : ControllerBase
{
    private const string ReadPermission = "admin.configuration.read";
    private const string ManageSuffix = ".manage";
    private const string ManagePrefix = "admin.configuration.";

    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User)
        ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid? ActorUserId => AuthenticatedRequestContext.GetUserId(User);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!HasAnyConfigPermission(ReadPermission))
            return Forbid();

        var categories = await service.GetForTenantAsync(TenantId, cancellationToken);
        return Ok(categories);
    }

    [HttpPut]
    public async Task<IActionResult> Set([FromBody] SetTenantConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return BadRequest("A configuration key is required.");

        // A caller may manage a key if they hold any admin.configuration.*.manage permission.
        if (!HasManagePermission())
            return Forbid();

        try
        {
            await service.SetTenantValueAsync(TenantId, ActorUserId, request.Key, request.ValueJson, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private bool HasAnyConfigPermission(string read)
    {
        var granted = AuthenticatedRequestContext.GetGrantedPermissions(User);
        return granted.Contains("NAV_ALL")
            || granted.Contains(read, StringComparer.OrdinalIgnoreCase)
            || granted.Any(p => p.StartsWith(ManagePrefix, StringComparison.OrdinalIgnoreCase));
    }

    private bool HasManagePermission()
    {
        var granted = AuthenticatedRequestContext.GetGrantedPermissions(User);
        return granted.Contains("NAV_ALL")
            || granted.Any(p => p.StartsWith(ManagePrefix, StringComparison.OrdinalIgnoreCase)
                && p.EndsWith(ManageSuffix, StringComparison.OrdinalIgnoreCase));
    }
}
