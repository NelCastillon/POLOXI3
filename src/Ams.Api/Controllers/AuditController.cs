using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Audit;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditService _service;

    public AuditController(IAuditService service)
    {
        _service = service;
    }

    // ── CRUD history ─────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("entity-history")]
    public async Task<IActionResult> GetEntityHistory([FromQuery] Guid tenantId, [FromQuery] string entityName, [FromQuery] Guid entityId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetEntityHistoryAsync(tenantId, entityName, entityId, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    // ── Field-level change tracking ──────────────────────────

    [HttpGet("field-changes")]
    public async Task<IActionResult> SearchFieldChanges([FromQuery] Guid tenantId, [FromQuery] string? entityName, [FromQuery] Guid? entityId, [FromQuery] string? fieldName, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchFieldChangesAsync(tenantId, entityName, entityId, fieldName, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    // ── Approval history ─────────────────────────────────────

    [HttpGet("approval-history")]
    public async Task<IActionResult> SearchApprovalHistory([FromQuery] Guid tenantId, [FromQuery] Guid? workflowInstanceId, [FromQuery] string? actionCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchApprovalHistoryAsync(tenantId, workflowInstanceId, actionCode, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    // ── Security events ──────────────────────────────────────

    [HttpGet("security-events")]
    public async Task<IActionResult> SearchSecurityEvents([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] bool? isSuccess, [FromQuery] string? eventTypeCode, [FromQuery] int? riskScoreMin, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchSecurityEventsAsync(tenantId, searchTerm, isSuccess, eventTypeCode, riskScoreMin, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("security-events/summary")]
    public async Task<IActionResult> GetSecurityEventSummary([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        var result = await _service.GetSecurityEventSummaryAsync(tenantId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("security-events/trends")]
    public async Task<IActionResult> GetSecurityEventTrends([FromQuery] Guid tenantId, [FromQuery] int days = 14, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetSecurityEventTrendAsync(tenantId, days, cancellationToken);
        return Ok(result);
    }

    // ── Export/download history ──────────────────────────────

    [HttpGet("export-logs")]
    public async Task<IActionResult> SearchExportLogs([FromQuery] Guid tenantId, [FromQuery] string? entityName, [FromQuery] string? exportTypeCode, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchExportLogsAsync(tenantId, entityName, exportTypeCode, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPost("export-logs")]
    public async Task<IActionResult> LogExport([FromBody] LogExportRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.LogExportAsync(request, cancellationToken);
        return Ok(id);
    }

    // ── Full record timeline ─────────────────────────────────

    [HttpGet("timeline/{entityName}/{entityId:guid}")]
    public async Task<IActionResult> GetRecordTimeline(string entityName, Guid entityId, [FromQuery] Guid tenantId, [FromQuery] int top = 100, CancellationToken cancellationToken = default)
    {
        var items = await _service.GetRecordTimelineAsync(tenantId, entityName, entityId, top, cancellationToken);
        return Ok(items);
    }

    // ── Retention policies ───────────────────────────────────

    [HttpGet("retention-policies")]
    public async Task<IActionResult> SearchRetentionPolicies([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.SearchRetentionPoliciesAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("retention-policies/{id:guid}")]
    public async Task<IActionResult> GetRetentionPolicy(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetRetentionPolicyByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("retention-policies")]
    public async Task<IActionResult> CreateRetentionPolicy([FromBody] CreateRetentionPolicyRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateRetentionPolicyAsync(request, cancellationToken);
        return Ok(id);
    }

    [HttpPut("retention-policies")]
    public async Task<IActionResult> UpdateRetentionPolicy([FromBody] UpdateRetentionPolicyRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateRetentionPolicyAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("retention-policies/{id:guid}/apply")]
    public async Task<IActionResult> ApplyRetentionPolicy(Guid id, CancellationToken cancellationToken)
    {
        var affected = await _service.ApplyRetentionPolicyAsync(id, cancellationToken);
        return Ok(new { AffectedRecords = affected });
    }

    // ── Write-path (event logging) ───────────────────────────

    [HttpPost("events")]
    public async Task<IActionResult> LogAuditEvent([FromBody] LogAuditEventRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.LogAuditEventAsync(request, cancellationToken);
        return Ok(id);
    }

    [HttpPost("field-changes/log")]
    public async Task<IActionResult> LogFieldChange([FromBody] LogFieldChangeRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.LogFieldChangeAsync(request, cancellationToken);
        return Ok(id);
    }

    [HttpPost("approval-history/log")]
    public async Task<IActionResult> LogApprovalHistory([FromBody] LogApprovalHistoryRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.LogApprovalHistoryAsync(request, cancellationToken);
        return Ok(id);
    }

    [HttpPost("security-events/log")]
    public async Task<IActionResult> LogSecurityEvent([FromBody] LogSecurityEventRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.LogSecurityEventAsync(request, cancellationToken);
        return Ok(id);
    }
}
