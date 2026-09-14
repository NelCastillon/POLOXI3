using Legal.Api.Security;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Science;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// POLOXI Formalization Gate endpoint. Converts a surviving research idea/hypothesis into a clean Proof
// Contract (Assumptions ⇒ Claim) that the Math Solver can attack directly (Research → Formalize → Math).
// The LLM only PROPOSES the contract; nothing here is treated as proven.
[ApiController]
[Route("api/intelligence_formalization")]
public sealed class IntelligenceFormalizationController(IFormalizationService service) : ControllerBase
{
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User) ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User) ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpPost("formalize")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Formalize([FromBody] FormalizationRequest request, CancellationToken cancellationToken) =>
        Ok(await service.FormalizeAsync(
            request with
            {
                TenantId = TenantId,
                UserId = ActorUserId,
                GrantedPermissions = AuthenticatedRequestContext.GetGrantedPermissions(User),
            },
            cancellationToken));
}
