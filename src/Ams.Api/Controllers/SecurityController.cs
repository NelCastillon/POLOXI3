using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Security;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/security")]
public sealed class SecurityController : ControllerBase
{
    private readonly IMfaDeviceService     _mfaService;
    private readonly ITrustedDeviceService _trustedDeviceService;
    private readonly IUserService          _userService;

    public SecurityController(IMfaDeviceService mfaService, ITrustedDeviceService trustedDeviceService, IUserService userService)
    {
        _mfaService           = mfaService;
        _trustedDeviceService = trustedDeviceService;
        _userService          = userService;
    }

    // ── MFA — user views ──────────────────────────────────────────────────────

    [HttpGet("mfa/users")]
    public async Task<IActionResult> SearchUsersWithMfa(
        [FromQuery] Guid tenantId,
        [FromQuery] string? searchTerm,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize   = 25,
        CancellationToken cancellationToken = default)
        => Ok(await _mfaService.SearchUsersWithMfaAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("mfa/users/without")]
    public async Task<IActionResult> SearchUsersWithoutMfa(
        [FromQuery] Guid tenantId,
        [FromQuery] string? searchTerm,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize   = 25,
        CancellationToken cancellationToken = default)
        => Ok(await _mfaService.SearchUsersWithoutMfaAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("mfa/users/{userId:guid}/devices")]
    public async Task<IActionResult> GetUserDevices(Guid userId, CancellationToken cancellationToken)
        => Ok(await _mfaService.GetUserDevicesAsync(userId, cancellationToken));

    // ── MFA — device actions ──────────────────────────────────────────────────

    [HttpPost("mfa/devices")]
    public async Task<IActionResult> AddMethod([FromBody] AddMfaMethodRequest request, CancellationToken cancellationToken)
    {
        var id = await _mfaService.AddMethodAsync(request, cancellationToken);
        return Ok(new { id });
    }

    [HttpPatch("mfa/devices/{id:guid}/verify")]
    public async Task<IActionResult> VerifyMethod(Guid id, [FromQuery] Guid? verifiedByUserId, CancellationToken cancellationToken)
    {
        await _mfaService.VerifyMethodAsync(new VerifyMfaMethodRequest { MfaDeviceId = id, VerifiedByUserId = verifiedByUserId }, cancellationToken);
        return NoContent();
    }

    [HttpPatch("mfa/devices/{id:guid}/disable")]
    public async Task<IActionResult> DisableMethod(Guid id, [FromQuery] Guid? disabledByUserId, CancellationToken cancellationToken)
    {
        await _mfaService.DisableMethodAsync(new DisableMfaMethodRequest { MfaDeviceId = id, DisabledByUserId = disabledByUserId }, cancellationToken);
        return NoContent();
    }

    // ── MFA — user-level actions ──────────────────────────────────────────────

    [HttpPost("mfa/users/{userId:guid}/reset")]
    public async Task<IActionResult> ResetMfa(Guid userId, [FromQuery] Guid? resetByUserId, CancellationToken cancellationToken)
    {
        await _mfaService.ResetMfaAsync(new ResetMfaRequest { UserId = userId, ResetByUserId = resetByUserId }, cancellationToken);
        return NoContent();
    }

    [HttpPatch("mfa/users/{userId:guid}/require")]
    public async Task<IActionResult> RequireMfa(Guid userId, [FromQuery] bool isRequired, [FromQuery] Guid? setByUserId, CancellationToken cancellationToken)
    {
        await _mfaService.RequireMfaAsync(new RequireMfaRequest { UserId = userId, IsRequired = isRequired, SetByUserId = setByUserId }, cancellationToken);
        return NoContent();
    }

    // ── Trusted Devices ───────────────────────────────────────────────────────

    [HttpGet("trusted-devices")]
    public async Task<IActionResult> SearchTrustedDevices(
        [FromQuery] Guid    tenantId,
        [FromQuery] Guid?   userId       = null,
        [FromQuery] string? searchTerm   = null,
        [FromQuery] bool?   isActive     = null,
        [FromQuery] bool?   highRiskOnly = null,
        [FromQuery] int     pageNumber   = 1,
        [FromQuery] int     pageSize     = 25,
        CancellationToken   cancellationToken = default)
        => Ok(await _trustedDeviceService.SearchAsync(tenantId, userId, searchTerm, isActive, highRiskOnly, pageNumber, pageSize, cancellationToken));

    [HttpGet("trusted-devices/{id:guid}")]
    public async Task<IActionResult> GetTrustedDevice(Guid id, CancellationToken cancellationToken)
    {
        var item = await _trustedDeviceService.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPatch("trusted-devices/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeTrustedDevice(Guid id, [FromQuery] Guid? revokedByUserId, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        await _trustedDeviceService.RevokeAsync(
            new RevokeTrustedDeviceRequest { TrustedDeviceId = id, RevokedByUserId = revokedByUserId, Reason = reason },
            cancellationToken);
        return NoContent();
    }

    [HttpPatch("trusted-devices/{id:guid}/risk-review")]
    public async Task<IActionResult> SubmitRiskReview(Guid id, [FromBody] RiskReviewRequest request, CancellationToken cancellationToken)
    {
        request.TrustedDeviceId = id;
        await _trustedDeviceService.SubmitRiskReviewAsync(request, cancellationToken);
        return NoContent();
    }

    // ── User Status Management ────────────────────────────────────────────────

    [HttpGet("user-status")]
    public async Task<IActionResult> SearchUsersForStatus(
        [FromQuery] Guid    tenantId,
        [FromQuery] string? searchTerm  = null,
        [FromQuery] string? statusCode  = null,
        [FromQuery] int     pageNumber  = 1,
        [FromQuery] int     pageSize    = 25,
        CancellationToken   cancellationToken = default)
    {
        var result = await _userService.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
        if (!string.IsNullOrEmpty(statusCode))
        {
            var filtered = result.Items.Where(u => string.Equals(u.StatusCode, statusCode, StringComparison.OrdinalIgnoreCase)).ToList();
            return Ok(new { Items = filtered, TotalCount = filtered.Count, result.PageNumber, result.PageSize });
        }
        return Ok(result);
    }

    [HttpPatch("user-status/{userId:guid}/change")]
    public async Task<IActionResult> ChangeUserStatus(Guid userId, [FromBody] ChangeUserStatusRequest request, CancellationToken cancellationToken)
    {
        request.UserId = userId;
        await _userService.ChangeStatusAsync(request, cancellationToken);
        return NoContent();
    }
}
