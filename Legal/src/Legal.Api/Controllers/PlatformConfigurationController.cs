using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// Platform Configuration control plane (/platform/configuration). Reads/writes the
// DB-backed platform-scoped defaults every tenant inherits. Super Admin only —
// gated by the platform.configuration.* permissions seeded in migration 0248.
// admin.configuration.* can never satisfy these gates. Configuration never grants
// access; these endpoints only shape how already-permitted capabilities behave.
[ApiController]
[Route("api/platform_configuration")]
[Authorize]
public sealed class PlatformConfigurationController(IPlatformConfigurationService service) : ControllerBase
{
    private const string ReadPermission = "platform.configuration.read";
    private const string ManageSuffix = ".manage";
    private const string ManagePrefix = "platform.configuration.";

    private Guid? ActorUserId => AuthenticatedRequestContext.GetUserId(User);

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!HasReadPermission())
            return Forbid();

        var categories = await service.GetAllAsync(cancellationToken);
        return Ok(categories);
    }

    [HttpPut]
    public async Task<IActionResult> Set([FromBody] SetPlatformConfigurationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return BadRequest("A configuration key is required.");

        if (!HasManagePermission())
            return Forbid();

        try
        {
            await service.SetPlatformValueAsync(ActorUserId, request.Key, request.ValueJson, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private bool HasReadPermission()
    {
        var granted = AuthenticatedRequestContext.GetGrantedPermissions(User);
        return granted.Contains("NAV_ALL")
            || granted.Contains(ReadPermission, StringComparer.OrdinalIgnoreCase)
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
