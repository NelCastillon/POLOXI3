using Ams.Api.Security;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/document-intake/operations")]
[Authorize(Policy=DocumentIntakePolicies.Admin)]
public sealed class DocumentIntakeOperationsController(IDocumentIntakeOperationsService service):ControllerBase
{
    private Guid TenantId=>AuthenticatedRequestContext.GetTenantId(User)??throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId=>AuthenticatedRequestContext.GetUserId(User)??throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)=>Ok(await service.GetSettingsAsync(TenantId,cancellationToken));

    [HttpGet("dead-letters")]
    public async Task<IActionResult> GetDeadLetters([FromQuery]int pageSize=100,CancellationToken cancellationToken=default)=>Ok(await service.GetDeadLettersAsync(TenantId,pageSize,cancellationToken));

    [HttpPost("dead-letters/{workItemId:guid}/replay")]
    public async Task<IActionResult> Replay(Guid workItemId,[FromBody]ReplayDocumentIntakeWorkCommand command,CancellationToken cancellationToken)
    {
        await service.ReplayDeadLetterAsync(command with{TenantId=TenantId,WorkItemId=workItemId,ActorUserId=ActorUserId},cancellationToken);
        return NoContent();
    }

    [HttpPost("sessions/{sessionId:guid}/legal-holds")]
    public async Task<IActionResult> PlaceLegalHold(Guid sessionId,[FromBody]PlaceDocumentIntakeLegalHoldCommand command,CancellationToken cancellationToken)
    {
        await service.PlaceLegalHoldAsync(command with{TenantId=TenantId,IntakeSessionId=sessionId,ActorUserId=ActorUserId},cancellationToken);
        return NoContent();
    }

    [HttpPost("legal-holds/{legalHoldId:guid}/release")]
    public async Task<IActionResult> ReleaseLegalHold(Guid legalHoldId,[FromBody]ReleaseDocumentIntakeLegalHoldCommand command,CancellationToken cancellationToken)
    {
        await service.ReleaseLegalHoldAsync(command with{TenantId=TenantId,LegalHoldId=legalHoldId,ActorUserId=ActorUserId},cancellationToken);
        return NoContent();
    }

    [HttpGet("prompt-suites")]
    public async Task<IActionResult> GetPromptSuites(CancellationToken cancellationToken)=>Ok(await service.GetPromptSuitesAsync(TenantId,cancellationToken));

    [HttpGet("prompt-runs")]
    public async Task<IActionResult> GetPromptRuns([FromQuery]int pageSize=100,CancellationToken cancellationToken=default)=>Ok(await service.GetPromptEvaluationRunsAsync(TenantId,pageSize,cancellationToken));

    [HttpPost("prompt-runs")]
    public async Task<IActionResult> QueuePromptRun([FromBody]QueuePromptEvaluationCommand command,CancellationToken cancellationToken)
    {
        var id=await service.QueuePromptEvaluationAsync(command with{TenantId=TenantId,ActorUserId=ActorUserId},cancellationToken);
        return Accepted(new{id});
    }

    [HttpPost("prompts/{promptId:guid}/approve")]
    public async Task<IActionResult> ApprovePrompt(Guid promptId,[FromBody]ApproveDocumentIntakePromptCommand command,CancellationToken cancellationToken)
    {
        await service.ApprovePromptAsync(command with{TenantId=TenantId,PromptDefinitionId=promptId,ActorUserId=ActorUserId},cancellationToken);
        return NoContent();
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery]bool openOnly=true,CancellationToken cancellationToken=default)=>Ok(await service.GetAlertsAsync(TenantId,openOnly,cancellationToken));
}
