using Ams.Application.Abstractions.Services;
using Ams.Application.Features.PolicyCertificates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/certificate-workflow")]
public sealed class CertificateWorkflowController : ControllerBase
{
    private readonly ICertificateWorkflowService _service;

    public CertificateWorkflowController(ICertificateWorkflowService service) => _service = service;

    [HttpGet("workspace")]
    public async Task<IActionResult> GetWorkspace([FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetWorkspaceAsync(tenantId, cancellationToken));

    [HttpGet("certificates/{certificateId:guid}/audit")]
    public async Task<IActionResult> GetAudit(Guid certificateId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetAuditAsync(tenantId, certificateId, cancellationToken));

    [HttpGet("certificates/{certificateId:guid}/deliveries")]
    public async Task<IActionResult> GetDeliveries(Guid certificateId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
        => Ok(await _service.GetDeliveriesAsync(tenantId, certificateId, cancellationToken));

    [HttpGet("certificates/{certificateId:guid}/latest-document-version")]
    public async Task<IActionResult> GetLatestDocumentVersion(Guid certificateId, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var id = await _service.GetLatestGeneratedDocumentVersionIdAsync(tenantId, certificateId, cancellationToken);
        return id is null ? NotFound() : Ok(id);
    }

    [HttpPost("holders")]
    public async Task<IActionResult> UpsertHolder([FromBody] UpsertCertificateHolderRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.UpsertHolderAsync(request, cancellationToken);
        return Ok(new { id });
    }

    [HttpPost("templates/{templateDefinitionId:guid}/versions")]
    public async Task<IActionResult> CreateTemplateVersion(Guid templateDefinitionId, [FromBody] CreateDocumentTemplateVersionRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateTemplateVersionAsync(request with { DocumentTemplateDefinitionId = templateDefinitionId }, cancellationToken);
        return Ok(new { id });
    }

    [HttpPost("requests")]
    public async Task<IActionResult> CreateRequest([FromBody] CreateCertificateWorkflowRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateRequestAsync(request, cancellationToken);
        return Created($"api/certificate-workflow/requests/{id}", new { id });
    }

    [HttpPost("certificates/{certificateId:guid}/generate")]
    public async Task<IActionResult> Generate(Guid certificateId, [FromBody] GenerateCertificateDocumentRequest request, CancellationToken cancellationToken)
    {
        var generated = await _service.GenerateAsync(request with { CertificateId = certificateId }, cancellationToken);
        return File(generated.Content, generated.ContentType, $"certificate-{certificateId}-v{generated.VersionNumber}.html");
    }

    [HttpPost("certificates/{certificateId:guid}/deliveries")]
    public async Task<IActionResult> QueueDelivery(Guid certificateId, [FromBody] QueueCertificateDeliveryRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.QueueDeliveryAsync(request with { CertificateId = certificateId }, cancellationToken);
        return Accepted(new { id });
    }

    [HttpPut("certificates/{certificateId:guid}/renewal-schedule")]
    public async Task<IActionResult> UpsertRenewalSchedule(Guid certificateId, [FromBody] UpsertCertificateRenewalScheduleRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.UpsertRenewalScheduleAsync(request with { CertificateId = certificateId }, cancellationToken);
        return Ok(new { id });
    }
}