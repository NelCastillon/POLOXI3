using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Saas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// ─────────────────────────────────────────────────────────────────────────────
// Public invitation acceptance surface (/invitations).
//
// Anonymous by design: an invitee arriving from an emailed accept link is not
// yet authenticated (and may not even have an account). Both endpoints operate
// solely on the single-use raw token — never a tenant/user identity from the
// caller — so they are safe to expose without authorization. The token is hashed
// server-side and matched against SaaS_TenantInvitation.TokenHash; no plaintext
// token is ever stored. Lookup returns only non-sensitive display data.
// ─────────────────────────────────────────────────────────────────────────────
[ApiController]
[Route("api/invitations")]
[AllowAnonymous]
public sealed class InvitationsController(IInvitationService invitationService) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<IActionResult> Lookup(string token, CancellationToken cancellationToken)
    {
        var result = await invitationService.LookupByTokenAsync(token, cancellationToken);
        if (result is null)
            return NotFound("This invitation link is invalid or has been removed.");
        return Ok(result);
    }

    [HttpPost("accept")]
    public async Task<IActionResult> Accept([FromBody] AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await invitationService.AcceptInvitationAsync(request, cancellationToken);
            return result.Outcome switch
            {
                InvitationAcceptOutcome.Success => Ok(result),
                InvitationAcceptOutcome.AlreadyMember => Ok(result),
                InvitationAcceptOutcome.AccountDetailsRequired => UnprocessableEntity(result),
                InvitationAcceptOutcome.InvalidToken => NotFound(result),
                _ => BadRequest(result)
            };
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
