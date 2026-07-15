using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Submissions;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/submissions/workflow-configuration")]
public sealed class SubmissionWorkflowConfigurationController : ControllerBase
{
    private readonly ISubmissionWorkflowConfigurationService _service;

    public SubmissionWorkflowConfigurationController(ISubmissionWorkflowConfigurationService service)
    {
        _service = service;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetSummaryAsync(tenantId, cancellationToken));

    [HttpGet("intake-templates")]
    public async Task<IActionResult> GetIntakeTemplates([FromQuery] Guid tenantId, [FromQuery] string? lineOfBusiness, CancellationToken cancellationToken)
        => Ok(await _service.GetIntakeTemplatesAsync(tenantId, lineOfBusiness, cancellationToken));

    [HttpPost("intake-templates")]
    public async Task<IActionResult> CreateIntakeTemplate([FromBody] UpsertSubmissionIntakeTemplateRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.UpsertIntakeTemplateAsync(null, request, cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("intake-templates/{id:guid}")]
    public async Task<IActionResult> UpdateIntakeTemplate(Guid id, [FromBody] UpsertSubmissionIntakeTemplateRequest request, CancellationToken cancellationToken)
    {
        await _service.UpsertIntakeTemplateAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("intake-templates/{id:guid}")]
    public async Task<IActionResult> DeleteIntakeTemplate(Guid id, [FromQuery] Guid tenantId, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        await _service.DeleteIntakeTemplateAsync(id, tenantId, userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("document-requirements")]
    public async Task<IActionResult> GetDocumentRequirements([FromQuery] Guid tenantId, [FromQuery] string? lineOfBusiness, CancellationToken cancellationToken)
        => Ok(await _service.GetDocumentRequirementsAsync(tenantId, lineOfBusiness, cancellationToken));

    [HttpPost("document-requirements")]
    public async Task<IActionResult> CreateDocumentRequirement([FromBody] UpsertSubmissionDocumentRequirementRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.UpsertDocumentRequirementAsync(null, request, cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("document-requirements/{id:guid}")]
    public async Task<IActionResult> UpdateDocumentRequirement(Guid id, [FromBody] UpsertSubmissionDocumentRequirementRequest request, CancellationToken cancellationToken)
    {
        await _service.UpsertDocumentRequirementAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("document-requirements/{id:guid}")]
    public async Task<IActionResult> DeleteDocumentRequirement(Guid id, [FromQuery] Guid tenantId, [FromQuery] Guid? userId, CancellationToken cancellationToken)
    {
        await _service.DeleteDocumentRequirementAsync(id, tenantId, userId, cancellationToken);
        return NoContent();
    }
}
