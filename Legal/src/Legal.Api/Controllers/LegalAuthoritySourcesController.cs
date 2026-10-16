using Legal.Api.Security;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Legal.Api.Controllers;

// Administration CRUD surface for the DB-backed Legal Authority Source registry
// (POLOXI.Legal_AuthoritySource). Lets administrators curate per
// Provider × Jurisdiction × AuthorityKind × MatterType official-law sources that
// close state statutory/regulatory coverage gaps. Mirrors the api/intelligence
// configuration conventions (tenant/actor from the authenticated context).
[ApiController]
[Route("api/intelligence/legal-authority-sources")]
public sealed class LegalAuthoritySourcesController(IIntelligenceWideRepository repository):ControllerBase
{
    private Guid TenantId=>AuthenticatedRequestContext.GetTenantId(User)??throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId=>AuthenticatedRequestContext.GetUserId(User)??throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpGet]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        =>Ok(await repository.GetLegalAuthoritySourcesAdminAsync(TenantId,cancellationToken));

    [HttpPost]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Create([FromBody]SaveLegalAuthoritySourceRequest request,CancellationToken cancellationToken)
    {
        var id=await repository.CreateLegalAuthoritySourceAsync(request,TenantId,ActorUserId,cancellationToken);
        return Ok(new{legalAuthoritySourceId=id});
    }

    [HttpPut("{legalAuthoritySourceId:guid}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Update(Guid legalAuthoritySourceId,[FromBody]SaveLegalAuthoritySourceRequest request,CancellationToken cancellationToken)
    {
        await repository.UpdateLegalAuthoritySourceAsync(legalAuthoritySourceId,request,TenantId,ActorUserId,cancellationToken);
        return NoContent();
    }

    [HttpPatch("{legalAuthoritySourceId:guid}/enabled")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Toggle(Guid legalAuthoritySourceId,[FromBody]ToggleLegalAuthoritySourceRequest request,CancellationToken cancellationToken)
    {
        await repository.ToggleLegalAuthoritySourceAsync(legalAuthoritySourceId,request.IsEnabled,TenantId,ActorUserId,cancellationToken);
        return NoContent();
    }

    [HttpDelete("{legalAuthoritySourceId:guid}")]
    [Authorize(Policy=IntelligencePolicies.Configure)]
    public async Task<IActionResult> Delete(Guid legalAuthoritySourceId,CancellationToken cancellationToken)
    {
        await repository.DeleteLegalAuthoritySourceAsync(legalAuthoritySourceId,TenantId,ActorUserId,cancellationToken);
        return NoContent();
    }
}
