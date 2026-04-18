using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Audit;

namespace Ams.Application.Abstractions.Persistence;

public interface IAuditRepository
{
    // ── CRUD history (AuditLog) ──────────────────────────────
    Task<AuditLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AuditLogDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PagedResult<AuditLogDto>> GetEntityHistoryAsync(Guid tenantId, string entityName, Guid entityId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<Guid> LogAuditEventAsync(LogAuditEventRequest request, CancellationToken cancellationToken = default);

    // ── Field-level change tracking ──────────────────────────
    Task<PagedResult<FieldChangeLogDto>> SearchFieldChangesAsync(Guid tenantId, string? entityName, Guid? entityId, string? fieldName, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> LogFieldChangeAsync(LogFieldChangeRequest request, CancellationToken cancellationToken = default);

    // ── Approval history ─────────────────────────────────────
    Task<PagedResult<WorkflowApprovalHistoryDto>> SearchApprovalHistoryAsync(Guid tenantId, Guid? workflowInstanceId, string? actionCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> LogApprovalHistoryAsync(LogApprovalHistoryRequest request, CancellationToken cancellationToken = default);

    // ── Security events ──────────────────────────────────────
    Task<PagedResult<SecurityEventLogDto>> SearchSecurityEventsAsync(Guid tenantId, string? searchTerm, bool? isSuccess, string? eventTypeCode = null, int? riskScoreMin = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<SecurityEventSummaryDto> GetSecurityEventSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SecurityEventTrendDto>> GetSecurityEventTrendAsync(Guid tenantId, int days = 14, CancellationToken cancellationToken = default);
    Task<Guid> LogSecurityEventAsync(LogSecurityEventRequest request, CancellationToken cancellationToken = default);

    // ── Export/download history ──────────────────────────────
    Task<PagedResult<ExportLogDto>> SearchExportLogsAsync(Guid tenantId, string? entityName, string? exportTypeCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> LogExportAsync(LogExportRequest request, CancellationToken cancellationToken = default);

    // ── Full record timeline ─────────────────────────────────
    Task<IReadOnlyList<RecordTimelineEntryDto>> GetRecordTimelineAsync(Guid tenantId, string entityName, Guid entityId, int top = 100, CancellationToken cancellationToken = default);

    // ── Retention policies ───────────────────────────────────
    Task<PagedResult<RetentionPolicyDto>> SearchRetentionPoliciesAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<RetentionPolicyDto?> GetRetentionPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateRetentionPolicyAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken = default);
    Task UpdateRetentionPolicyAsync(UpdateRetentionPolicyRequest request, CancellationToken cancellationToken = default);
    Task<int> ApplyRetentionPolicyAsync(Guid retentionPolicyId, CancellationToken cancellationToken = default);
}
