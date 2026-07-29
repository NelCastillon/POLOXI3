using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/proposal-provider-callbacks")]
public sealed class ProposalProviderCallbacksController : ControllerBase
{
    private readonly ISubmissionService _service;

    public ProposalProviderCallbacksController(ISubmissionService service) => _service = service;

    [HttpPost("{providerCode}")]
    public async Task<IActionResult> Ingest(string providerCode, [FromQuery] Guid tenantId, [FromBody] ProposalProviderCallbackPayload payload, CancellationToken cancellationToken)
    {
        var signature = Request.Headers["X-AMS-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(signature)) return Unauthorized();
        try
        {
            var id = await _service.ProcessProposalProviderCallbackAsync(new ProposalProviderCallbackRequest(
                tenantId,
                providerCode,
                payload.ProviderEventId,
                payload.ExternalEnvelopeId,
                payload.EventTypeCode,
                payload.StatusCode,
                payload.PayloadJson,
                signature,
                payload.SignedDocumentId,
                payload.CertificateDocumentId), cancellationToken);
            return Ok(new { id });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new ProblemDetails { Status = StatusCodes.Status409Conflict, Title = "Proposal callback could not be processed", Detail = exception.Message });
        }
    }
}

public sealed record ProposalProviderCallbackPayload(
    string ProviderEventId,
    string? ExternalEnvelopeId,
    string EventTypeCode,
    string StatusCode,
    string PayloadJson,
    Guid? SignedDocumentId = null,
    Guid? CertificateDocumentId = null);
