using Ams.Api.Security;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.DocumentIntake;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/document-intake")]
public sealed class DocumentIntakeController : ControllerBase
{
    private readonly IDocumentIntakeService _service;
    public DocumentIntakeController(IDocumentIntakeService service) => _service = service;

    private Guid TenantId => AuthenticatedRequestContext.GetTenantId(User)
        ?? throw new UnauthorizedAccessException("An authenticated tenant context is required.");
    private Guid ActorUserId => AuthenticatedRequestContext.GetUserId(User)
        ?? throw new UnauthorizedAccessException("An authenticated user context is required.");

    [HttpGet]
    [Authorize(Policy = DocumentIntakePolicies.Read)]
    public async Task<IActionResult> Search([FromQuery] string? searchTerm,[FromQuery] string? moduleCode,[FromQuery] string? statusCode,[FromQuery] Guid? assignedToUserId,[FromQuery] Guid? targetEntityId,[FromQuery] int pageNumber=1,[FromQuery] int pageSize=50,CancellationToken cancellationToken=default)
        => Ok(await _service.SearchAsync(TenantId,searchTerm,moduleCode,statusCode,assignedToUserId,targetEntityId,pageNumber,pageSize,cancellationToken));

    [HttpGet("document-statuses")]
    [Authorize(Policy = DocumentIntakePolicies.Read)]
    public async Task<IActionResult> GetDocumentStatuses([FromQuery] string moduleCode,[FromQuery] Guid targetEntityId,CancellationToken cancellationToken=default)
        => Ok(await _service.GetDocumentStatusesAsync(TenantId,moduleCode,targetEntityId,cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = DocumentIntakePolicies.Read)]
    public async Task<IActionResult> Get(Guid id,CancellationToken cancellationToken)
    {
        var result=await _service.GetAsync(TenantId,id,cancellationToken);
        return result is null?NotFound():Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = DocumentIntakePolicies.Upload)]
    public async Task<IActionResult> Create([FromBody] CreateDocumentIntakeSessionCommand command,CancellationToken cancellationToken)
    {
        var secured=command with{TenantId=TenantId,CreatedByUserId=ActorUserId};
        var id=await _service.CreateAsync(secured,cancellationToken);
        return CreatedAtAction(nameof(Get),new{id},new{id});
    }

    [HttpPost("{id:guid}/documents")]
    [Authorize(Policy = DocumentIntakePolicies.Upload)]
    public async Task<IActionResult> AttachDocument(Guid id,[FromBody] AttachDocumentToIntakeCommand command,CancellationToken cancellationToken)
    {
        await _service.AttachDocumentAsync(command with{TenantId=TenantId,IntakeSessionId=id,ActorUserId=ActorUserId},cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/queue")]
    [Authorize(Policy = DocumentIntakePolicies.Upload)]
    public async Task<IActionResult> Queue(Guid id,[FromBody] QueueDocumentIntakeCommand command,CancellationToken cancellationToken)
    {
        await _service.QueueAsync(command with{TenantId=TenantId,IntakeSessionId=id,ActorUserId=ActorUserId},cancellationToken);
        return AcceptedAtAction(nameof(Get),new{id},null);
    }

    [HttpPut("{id:guid}/fields/{fieldId:guid}/review")]
    [Authorize(Policy = DocumentIntakePolicies.Review)]
    public async Task<IActionResult> ReviewField(Guid id,Guid fieldId,[FromBody] ReviewDocumentIntakeFieldCommand command,CancellationToken cancellationToken)
    {
        await _service.ReviewFieldAsync(command with{TenantId=TenantId,IntakeSessionId=id,IntakeDraftFieldId=fieldId,ReviewerUserId=ActorUserId},cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/issues/{issueId:guid}/resolve")]
    [Authorize(Policy = DocumentIntakePolicies.Review)]
    public async Task<IActionResult> ResolveIssue(Guid id,Guid issueId,[FromBody] ResolveDocumentIntakeIssueCommand command,CancellationToken cancellationToken)
    {
        await _service.ResolveIssueAsync(command with{TenantId=TenantId,IntakeSessionId=id,IntakeIssueId=issueId,ReviewerUserId=ActorUserId},cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reprocess")]
    [Authorize(Policy = DocumentIntakePolicies.Reprocess)]
    public async Task<IActionResult> Reprocess(Guid id,[FromBody] ReprocessDocumentIntakeCommand command,CancellationToken cancellationToken)
    {
        await _service.ReprocessAsync(command with{TenantId=TenantId,IntakeSessionId=id,ActorUserId=ActorUserId},cancellationToken);
        return AcceptedAtAction(nameof(Get),new{id},null);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = DocumentIntakePolicies.Review)]
    public async Task<IActionResult> Cancel(Guid id,[FromBody] CancelDocumentIntakeCommand command,CancellationToken cancellationToken)
    {
        await _service.CancelAsync(command with{TenantId=TenantId,IntakeSessionId=id,ActorUserId=ActorUserId},cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/promote")]
    [Authorize(Policy = DocumentIntakePolicies.Promote)]
    public async Task<IActionResult> Promote(Guid id,[FromBody] PromoteDocumentIntakeCommand command,CancellationToken cancellationToken)
        => Ok(await _service.PromoteAsync(command with{TenantId=TenantId,IntakeSessionId=id,ActorUserId=ActorUserId},cancellationToken));
}
