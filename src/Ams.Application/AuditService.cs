using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Audit;

namespace Ams.Application;

public sealed class AuditService : IAuditService
{
    private readonly IAuditRepository _repository;

    public AuditService(IAuditRepository repository)
    {
        _repository = repository;
    }

    // ── CRUD history ─────────────────────────────────────────
    public Task<AuditLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    public Task<PagedResult<AuditLogDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<PagedResult<AuditLogDto>> GetEntityHistoryAsync(Guid tenantId, string entityName, Guid entityId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _repository.GetEntityHistoryAsync(tenantId, entityName, entityId, pageNumber, pageSize, cancellationToken);

    public Task<Guid> LogAuditEventAsync(LogAuditEventRequest request, CancellationToken cancellationToken = default)
        => _repository.LogAuditEventAsync(request, cancellationToken);

    // ── Field-level change tracking ──────────────────────────
    public Task<PagedResult<FieldChangeLogDto>> SearchFieldChangesAsync(Guid tenantId, string? entityName, Guid? entityId, string? fieldName, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchFieldChangesAsync(tenantId, entityName, entityId, fieldName, pageNumber, pageSize, cancellationToken);

    public Task<Guid> LogFieldChangeAsync(LogFieldChangeRequest request, CancellationToken cancellationToken = default)
        => _repository.LogFieldChangeAsync(request, cancellationToken);

    // ── Approval history ─────────────────────────────────────
    public Task<PagedResult<WorkflowApprovalHistoryDto>> SearchApprovalHistoryAsync(Guid tenantId, Guid? workflowInstanceId, string? actionCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchApprovalHistoryAsync(tenantId, workflowInstanceId, actionCode, pageNumber, pageSize, cancellationToken);

    public Task<Guid> LogApprovalHistoryAsync(LogApprovalHistoryRequest request, CancellationToken cancellationToken = default)
        => _repository.LogApprovalHistoryAsync(request, cancellationToken);

    // ── Security events ──────────────────────────────────────
    public Task<PagedResult<SecurityEventLogDto>> SearchSecurityEventsAsync(Guid tenantId, string? searchTerm, bool? isSuccess, string? eventTypeCode = null, int? riskScoreMin = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchSecurityEventsAsync(tenantId, searchTerm, isSuccess, eventTypeCode, riskScoreMin, pageNumber, pageSize, cancellationToken);

    public Task<SecurityEventSummaryDto> GetSecurityEventSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetSecurityEventSummaryAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<SecurityEventTrendDto>> GetSecurityEventTrendAsync(Guid tenantId, int days = 14, CancellationToken cancellationToken = default)
        => _repository.GetSecurityEventTrendAsync(tenantId, days, cancellationToken);

    public Task<Guid> LogSecurityEventAsync(LogSecurityEventRequest request, CancellationToken cancellationToken = default)
        => _repository.LogSecurityEventAsync(request, cancellationToken);

    // ── Export/download history ──────────────────────────────
    public Task<PagedResult<ExportLogDto>> SearchExportLogsAsync(Guid tenantId, string? entityName, string? exportTypeCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchExportLogsAsync(tenantId, entityName, exportTypeCode, pageNumber, pageSize, cancellationToken);

    public Task<Guid> LogExportAsync(LogExportRequest request, CancellationToken cancellationToken = default)
        => _repository.LogExportAsync(request, cancellationToken);

    // ── Full record timeline ─────────────────────────────────
    public Task<IReadOnlyList<RecordTimelineEntryDto>> GetRecordTimelineAsync(Guid tenantId, string entityName, Guid entityId, int top = 100, CancellationToken cancellationToken = default)
        => _repository.GetRecordTimelineAsync(tenantId, entityName, entityId, top, cancellationToken);

    // ── Retention policies ───────────────────────────────────
    public Task<PagedResult<RetentionPolicyDto>> SearchRetentionPoliciesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchRetentionPoliciesAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken);

    public Task<RetentionPolicyDto?> GetRetentionPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetRetentionPolicyByIdAsync(id, cancellationToken);

    public Task<Guid> CreateRetentionPolicyAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
        => _repository.CreateRetentionPolicyAsync(request, cancellationToken);

    public Task UpdateRetentionPolicyAsync(UpdateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
        => _repository.UpdateRetentionPolicyAsync(request, cancellationToken);

    public Task<int> ApplyRetentionPolicyAsync(Guid retentionPolicyId, CancellationToken cancellationToken = default)
        => _repository.ApplyRetentionPolicyAsync(retentionPolicyId, cancellationToken);
}
