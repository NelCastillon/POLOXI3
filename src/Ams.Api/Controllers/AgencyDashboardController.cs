using Ams.Application.Abstractions.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/agency-dashboard")]
public sealed class AgencyDashboardController : ControllerBase
{
    private readonly IAgencyDashboardService _svc;
    public AgencyDashboardController(IAgencyDashboardService svc) => _svc = svc;

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await _svc.GetExecutiveOverviewAsync(tenantId, ct));

    [HttpGet("kpis")]
    public async Task<IActionResult> GetKpis([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await _svc.GetKpisAsync(tenantId, ct));

    [HttpGet("branch-performance")]
    public async Task<IActionResult> GetBranchPerformance([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await _svc.GetBranchPerformanceAsync(tenantId, ct));

    [HttpGet("producer-performance")]
    public async Task<IActionResult> GetProducerPerformance([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await _svc.GetProducerPerformanceAsync(tenantId, ct));

    [HttpGet("renewal-pipeline")]
    public async Task<IActionResult> GetRenewalPipeline([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await _svc.GetRenewalPipelineAsync(tenantId, ct));

    [HttpGet("claims-summary")]
    public async Task<IActionResult> GetClaimsSummary([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await _svc.GetClaimsSummaryAsync(tenantId, ct));

    [HttpGet("billing-summary")]
    public async Task<IActionResult> GetBillingSummary([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await _svc.GetBillingSummaryAsync(tenantId, ct));
}
