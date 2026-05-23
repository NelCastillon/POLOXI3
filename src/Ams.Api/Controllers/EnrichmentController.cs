using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Enrichment;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/enrichment")]
public sealed class EnrichmentController : ControllerBase
{
    private readonly IEnrichmentService _service;

    public EnrichmentController(IEnrichmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetWorkspace(
        [FromQuery] Guid tenantId,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? providerStatus = null,
        [FromQuery] string? jobStatus = null,
        [FromQuery] string? entityType = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetWorkspaceAsync(new EnrichmentSearchRequest
        {
            TenantId = tenantId,
            SearchTerm = searchTerm,
            ProviderStatus = providerStatus,
            JobStatus = jobStatus,
            EntityType = entityType
        }, cancellationToken);

        return Ok(result);
    }

    [HttpPut("providers/{providerId:guid}/configuration")]
    public async Task<IActionResult> ConfigureProvider(Guid providerId, [FromBody] EnrichmentProviderConfigRequest request, CancellationToken cancellationToken = default)
    {
        await _service.ConfigureProviderAsync(providerId, request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("providers/{providerId:guid}/status")]
    public async Task<IActionResult> SetProviderStatus(Guid providerId, [FromBody] EnrichmentProviderStatusRequest request, CancellationToken cancellationToken = default)
    {
        await _service.SetProviderStatusAsync(providerId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] EnrichmentRunRequest request, CancellationToken cancellationToken = default)
        => Ok(await _service.RunAsync(request, cancellationToken));
}
