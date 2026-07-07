using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _service;

    public AuthController(IAuthService service)
    {
        _service = service;
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.ValidateCredentialsAsync(
            request.TenantId,
            request.UserNameOrEmail,
            request.Password,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpPost("2fa/verify")]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request, CancellationToken cancellationToken)
    {
        var user = await _service.VerifyTwoFactorAsync(
            request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return user is null ? Unauthorized() : Ok(user);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterLoginUserRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.RegisterLoginUserAsync(request, cancellationToken);
        return Created($"api/users/{id}", new { id });
    }
}

public sealed class ValidateLoginRequest
{
    public Guid TenantId { get; set; }
    public string UserNameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
