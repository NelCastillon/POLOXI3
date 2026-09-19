using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Legal.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — public authentication surface.
//
// Signup issues a 6-digit email verification code; verification confirms the
// email, provisions the workspace idempotently, then normal password sign-in
// applies. Password recovery uses Identity's default token providers. All
// endpoints here are anonymous; authenticated flows live in the execution gateway.
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailVerificationService emailVerification,
    IWorkspaceProvisioningService provisioning,
    ITenantContextService tenantContext,
    IActivityService activity,
    IConsentService consent,
    IJudzEmailSender emailSender) : ControllerBase
{
    [HttpGet("agreements")]
    public async Task<IActionResult> GetAgreements(CancellationToken ct)
    {
        var agreements = await consent.GetActiveAgreementsAsync(ct);
        return Ok(agreements);
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup([FromBody] SignupRequest request, CancellationToken ct)
    {
        if (!request.AcceptedAgreements)
            return BadRequest(new { Message = "You must accept the Terms of Service and Privacy Policy to create an account." });

        var email = request.Email.Trim();
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Do not leak account existence; if unconfirmed, re-issue a code.
            if (!existing.EmailConfirmed)
                await emailVerification.IssueCodeAsync(existing.Id, email, ct);
            return Ok(new { Message = "If the email is available, a verification code has been sent." });
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmailConfirmed = false
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });

        // Record clickwrap consent evidence for every active agreement the user
        // accepted, capturing the exact document versions/hashes plus client IP,
        // user-agent, and a correlation id for a tamper-evident audit trail.
        await consent.RecordConsentForActiveAgreementsAsync(
            user.Id, null, email, ConsentAcceptanceMethod.ClickwrapCheckbox,
            ResolveClientIp(), ResolveUserAgent(), HttpContext.TraceIdentifier, ct);

        await emailVerification.IssueCodeAsync(user.Id, email, ct);
        return Ok(new { Message = "Account created. Check your email for the 6-digit verification code." });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return BadRequest(new { Message = "Invalid verification request." });

        var outcome = await emailVerification.VerifyCodeAsync(user.Id, request.Code.Trim(), ct);
        if (outcome != EmailVerificationOutcome.Success)
            return BadRequest(new { Outcome = outcome.ToString(), Message = "Verification failed." });

        user.EmailConfirmed = true;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
            return BadRequest(new { Errors = update.Errors.Select(e => e.Description) });

        var displayName = $"{user.FirstName} {user.LastName}".Trim();
        var tenantId = await provisioning.ProvisionAsync(user.Id, user.Email!, displayName, ct);
        return Ok(new { Message = "Email verified. Your workspace is ready.", TenantId = tenantId });
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null && !user.EmailConfirmed)
            await emailVerification.ResendCodeAsync(user.Id, user.Email!, ct);

        return Ok(new { Message = "If the email requires verification, a new code has been sent." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            await RecordLoginAsync(null, null, email, LoginOutcome.InvalidCredentials, ct);
            return Unauthorized(new { Message = "Invalid credentials." });
        }

        if (!user.EmailConfirmed)
        {
            await RecordLoginAsync(user.Id, null, email, LoginOutcome.EmailNotVerified, ct);
            return Unauthorized(new { Message = "Email not verified.", RequiresVerification = true });
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            await RecordLoginAsync(user.Id, null, email, LoginOutcome.LockedOut, ct);
            return Unauthorized(new { Message = "Account locked. Try again later." });
        }
        if (!result.Succeeded)
        {
            await RecordLoginAsync(user.Id, null, email, LoginOutcome.InvalidCredentials, ct);
            return Unauthorized(new { Message = "Invalid credentials." });
        }

        var membership = await tenantContext.ResolveMembershipAsync(user.Id, ct);
        var permissions = membership is null
            ? Array.Empty<string>()
            : (await tenantContext.ResolveEffectivePermissionsAsync(user.Id, membership.TenantId, ct)).ToArray();
        await RecordLoginAsync(user.Id, membership?.TenantId, email, LoginOutcome.Success, ct);
        return Ok(new
        {
            UserId = user.Id,
            user.Email,
            DisplayName = $"{user.FirstName} {user.LastName}".Trim(),
            TenantId = membership?.TenantId,
            Permissions = permissions
        });
    }

    private Task RecordLoginAsync(Guid? userId, Guid? tenantId, string email, string outcome, CancellationToken ct)
    {
        return activity.RecordLoginAsync(
            new RecordLoginRequest(userId, tenantId, email, outcome, outcome == LoginOutcome.Success, ResolveClientIp(), ResolveUserAgent()),
            ct);
    }

    private string? ResolveUserAgent()
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        if (userAgent.Length > 512)
            userAgent = userAgent[..512];
        return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
    }

    // Prefer the forwarded client address (behind the Blazor server-side ApiClient
    // call or a reverse proxy the connection IP is the proxy, not the end user).
    private string? ResolveClientIp()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // X-Forwarded-For is a comma-separated list; the first entry is the client.
            var first = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
                return first.Length > 64 ? first[..64] : first;
        }

        var realIp = Request.Headers["X-Real-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(realIp))
            return realIp.Length > 64 ? realIp[..64] : realIp;

        var remote = HttpContext.Connection.RemoteIpAddress;
        if (remote is null)
            return null;

        // Normalize IPv4-mapped IPv6 (e.g. ::ffff:203.0.113.24) and loopback.
        if (remote.IsIPv4MappedToIPv6)
            remote = remote.MapToIPv4();
        return remote.ToString();
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is not null && user.EmailConfirmed)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = Uri.EscapeDataString(token);
            var resetUrl = $"{Request.Scheme}://{Request.Host}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={encoded}";
            await emailSender.SendPasswordResetAsync(user.Email!, resetUrl, ct);
        }

        return Ok(new { Message = "If the email exists, a password reset link has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return BadRequest(new { Message = "Invalid reset request." });

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });

        return Ok(new { Message = "Password updated. You can now sign in." });
    }
}
