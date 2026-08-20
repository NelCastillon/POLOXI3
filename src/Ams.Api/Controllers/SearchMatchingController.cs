using Ams.Api.Security;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.SearchMatching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/search-matching")]
[Authorize]
public sealed class SearchMatchingController(
    IEntityMatchingService matchingService,
    ISearchMatchingRepository repository) : ControllerBase
{
    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User)
        ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");

    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User)
        ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpPost("match")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Match([FromBody] EntityMatchRequest request, CancellationToken cancellationToken)
    {
        request.TenantId = TenantId;
        request.RequestedByUserId = ActorUserId;
        return Ok(await matchingService.FindMatchesAsync(request, cancellationToken));
    }

    [HttpPost("module-match")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> ModuleMatch([FromBody] ModuleMatchRequest request, CancellationToken cancellationToken)
    {
        request.TenantId = TenantId;
        request.RequestedByUserId = ActorUserId;
        return Ok(await matchingService.FindModuleMatchesAsync(request, cancellationToken));
    }

    [HttpPost("search")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> Search([FromBody] EnterpriseFuzzySearchRequest request, CancellationToken cancellationToken)
    {
        request.TenantId = TenantId;
        request.RequestedByUserId = ActorUserId;
        request.GrantedPermissions = AuthenticatedRequestContext.GetGrantedPermissions(User);
        return Ok(await matchingService.SearchAsync(request, cancellationToken));
    }

    [HttpGet("profiles/{profileCode}")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetProfile(string profileCode, CancellationToken cancellationToken)
    {
        var profile = await repository.GetPolicyAsync(TenantId, profileCode, cancellationToken);
        return profile is null ? NotFound() : Ok(profile);
    }

    [HttpPost("review-decisions")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> SaveReviewDecision([FromBody] MatchReviewDecisionRequest request, CancellationToken cancellationToken)
    {
        request.TenantId = TenantId;
        request.RequestedByUserId = ActorUserId;
        return Ok(await repository.SaveReviewDecisionAsync(request, cancellationToken));
    }

    [HttpGet("executions/{matchExecutionId:guid}/review-decisions")]
    [Authorize(Policy = IntelligencePolicies.Search)]
    public async Task<IActionResult> GetReviewDecisions(Guid matchExecutionId, CancellationToken cancellationToken)
        => Ok(await repository.GetReviewDecisionsAsync(TenantId, matchExecutionId, cancellationToken));

    [HttpGet("administration")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> GetAdministration(CancellationToken cancellationToken)
        => Ok(await repository.GetAdministrationAsync(TenantId, cancellationToken));

    [HttpPost("administration/profiles")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveProfile([FromBody] SaveMatchProfileSettingRequest request, CancellationToken cancellationToken)
        => Ok(new { id = await repository.SaveProfileAsync(TenantId, ActorUserId, request, cancellationToken) });

    [HttpDelete("administration/profiles/{id:guid}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteProfile(Guid id, [FromQuery] string rowVersion, CancellationToken cancellationToken)
    {
        await repository.DeleteProfileAsync(TenantId, ActorUserId, id, Convert.FromBase64String(rowVersion), cancellationToken);
        return NoContent();
    }

    [HttpPost("administration/field-rules")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveFieldRule([FromBody] SaveMatchFieldRuleSettingRequest request, CancellationToken cancellationToken)
        => Ok(new { id = await repository.SaveFieldRuleAsync(TenantId, ActorUserId, request, cancellationToken) });

    [HttpDelete("administration/field-rules/{id:guid}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteFieldRule(Guid id, [FromQuery] string rowVersion, CancellationToken cancellationToken)
    {
        await repository.DeleteFieldRuleAsync(TenantId, ActorUserId, id, Convert.FromBase64String(rowVersion), cancellationToken);
        return NoContent();
    }

    [HttpPost("administration/algorithms")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveAlgorithm([FromBody] SaveMatchAlgorithmSettingRequest request, CancellationToken cancellationToken)
        => Ok(new { id = await repository.SaveAlgorithmAsync(TenantId, ActorUserId, request, cancellationToken) });

    [HttpDelete("administration/algorithms/{id:guid}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteAlgorithm(Guid id, [FromQuery] string rowVersion, CancellationToken cancellationToken)
    {
        await repository.DeleteAlgorithmAsync(TenantId, ActorUserId, id, Convert.FromBase64String(rowVersion), cancellationToken);
        return NoContent();
    }

    [HttpPost("administration/normalization-terms")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> SaveNormalizationTerm([FromBody] SaveNormalizationTermSettingRequest request, CancellationToken cancellationToken)
        => Ok(new { id = await repository.SaveNormalizationTermAsync(TenantId, ActorUserId, request, cancellationToken) });

    [HttpDelete("administration/normalization-terms/{id:guid}")]
    [Authorize(Policy = IntelligencePolicies.Configure)]
    public async Task<IActionResult> DeleteNormalizationTerm(Guid id, [FromQuery] string rowVersion, CancellationToken cancellationToken)
    {
        await repository.DeleteNormalizationTermAsync(TenantId, ActorUserId, id, Convert.FromBase64String(rowVersion), cancellationToken);
        return NoContent();
    }
}
