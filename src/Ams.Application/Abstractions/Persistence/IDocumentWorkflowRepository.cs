using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.DocumentWorkflow;

namespace Ams.Application.Abstractions.Persistence;

public interface IDocumentWorkflowRepository
{
    // ══════════════════════════════════════════════════════════════════════
    // WORKFLOW TEMPLATES
    // ══════════════════════════════════════════════════════════════════════

    Task<DocumentWorkflowTemplateDto?> GetWorkflowTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DocumentWorkflowTemplateDto?> GetWorkflowTemplateByCodeAsync(Guid tenantId, string templateCode, CancellationToken cancellationToken = default);
    Task<PagedResult<DocumentWorkflowTemplateDto>> SearchWorkflowTemplatesAsync(Guid tenantId, string? workflowType, bool? isActive, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentWorkflowTemplateDto>> GetActiveWorkflowTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> CreateWorkflowTemplateAsync(CreateWorkflowTemplateRequest request, CancellationToken cancellationToken = default);
    Task UpdateWorkflowTemplateAsync(UpdateWorkflowTemplateRequest request, CancellationToken cancellationToken = default);
    Task DeleteWorkflowTemplateAsync(DeleteWorkflowTemplateRequest request, CancellationToken cancellationToken = default);
    Task ActivateWorkflowTemplateAsync(ActivateWorkflowTemplateRequest request, CancellationToken cancellationToken = default);
    Task DeactivateWorkflowTemplateAsync(DeactivateWorkflowTemplateRequest request, CancellationToken cancellationToken = default);

    // ══════════════════════════════════════════════════════════════════════
    // WORKFLOW STEP TEMPLATES
    // ══════════════════════════════════════════════════════════════════════

    Task<IReadOnlyList<DocumentWorkflowStepTemplateDto>> GetStepTemplatesByWorkflowIdAsync(Guid workflowTemplateId, CancellationToken cancellationToken = default);
    Task<DocumentWorkflowStepTemplateDto?> GetStepTemplateByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateStepTemplateAsync(CreateWorkflowStepTemplateRequest request, CancellationToken cancellationToken = default);
    Task UpdateStepTemplateAsync(UpdateWorkflowStepTemplateRequest request, CancellationToken cancellationToken = default);
    Task DeleteStepTemplateAsync(DeleteWorkflowStepTemplateRequest request, CancellationToken cancellationToken = default);

    // ══════════════════════════════════════════════════════════════════════
    // WORKFLOW INSTANCES
    // ══════════════════════════════════════════════════════════════════════

    Task<DocumentWorkflowInstanceDto?> GetWorkflowInstanceByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<DocumentWorkflowInstanceDto>> SearchWorkflowInstancesAsync(Guid tenantId, string? workflowStatus, Guid? documentId, Guid? initiatedByUserId, DateTime? startDateFrom, DateTime? startDateTo, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentWorkflowInstanceDto>> GetWorkflowInstancesByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentWorkflowInstanceDto>> GetActiveWorkflowInstancesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> CreateWorkflowInstanceAsync(CreateWorkflowInstanceRequest request, CancellationToken cancellationToken = default);
    Task StartWorkflowInstanceAsync(StartWorkflowInstanceRequest request, CancellationToken cancellationToken = default);
    Task AdvanceWorkflowInstanceAsync(AdvanceWorkflowInstanceRequest request, CancellationToken cancellationToken = default);
    Task CompleteWorkflowInstanceAsync(CompleteWorkflowInstanceRequest request, CancellationToken cancellationToken = default);
    Task RejectWorkflowInstanceAsync(RejectWorkflowInstanceRequest request, CancellationToken cancellationToken = default);
    Task CancelWorkflowInstanceAsync(CancelWorkflowInstanceRequest request, CancellationToken cancellationToken = default);
    Task EscalateWorkflowInstanceAsync(EscalateWorkflowInstanceRequest request, CancellationToken cancellationToken = default);

    // ══════════════════════════════════════════════════════════════════════
    // APPROVALS
    // ══════════════════════════════════════════════════════════════════════

    Task<DocumentApprovalDto?> GetApprovalByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<DocumentApprovalDto>> SearchApprovalsAsync(Guid tenantId, string? approvalStatus, Guid? assignedToUserId, Guid? documentId, DateTime? dueDateFrom, DateTime? dueDateTo, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentApprovalDto>> GetApprovalsByWorkflowInstanceIdAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentApprovalDto>> GetPendingApprovalsByUserIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<Guid> CreateApprovalAsync(CreateApprovalRequest request, CancellationToken cancellationToken = default);
    Task ApproveDocumentAsync(ApproveDocumentRequest request, CancellationToken cancellationToken = default);
    Task RejectDocumentAsync(RejectDocumentRequest request, CancellationToken cancellationToken = default);
    Task DeferApprovalAsync(DeferApprovalRequest request, CancellationToken cancellationToken = default);
    Task EscalateApprovalAsync(EscalateApprovalRequest request, CancellationToken cancellationToken = default);
    Task ReassignApprovalAsync(ReassignApprovalRequest request, CancellationToken cancellationToken = default);

    // ══════════════════════════════════════════════════════════════════════
    // REVIEWS
    // ══════════════════════════════════════════════════════════════════════

    Task<DocumentReviewDto?> GetReviewByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<DocumentReviewDto>> SearchReviewsAsync(Guid tenantId, string? reviewStatus, Guid? assignedToUserId, Guid? documentId, DateTime? dueDateFrom, DateTime? dueDateTo, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentReviewDto>> GetReviewsByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentReviewDto>> GetPendingReviewsByUserIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<Guid> CreateReviewAsync(CreateReviewRequest request, CancellationToken cancellationToken = default);
    Task StartReviewAsync(StartReviewRequest request, CancellationToken cancellationToken = default);
    Task CompleteReviewAsync(CompleteReviewRequest request, CancellationToken cancellationToken = default);
    Task ReturnReviewAsync(ReturnReviewRequest request, CancellationToken cancellationToken = default);
    Task CancelReviewAsync(CancelReviewRequest request, CancellationToken cancellationToken = default);
    Task ReassignReviewAsync(ReassignReviewRequest request, CancellationToken cancellationToken = default);

    // ══════════════════════════════════════════════════════════════════════
    // RETENTION POLICIES
    // ══════════════════════════════════════════════════════════════════════

    Task<DocumentRetentionPolicyDto?> GetRetentionPolicyByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DocumentRetentionPolicyDto?> GetRetentionPolicyByCodeAsync(Guid tenantId, string policyCode, CancellationToken cancellationToken = default);
    Task<PagedResult<DocumentRetentionPolicyDto>> SearchRetentionPoliciesAsync(Guid tenantId, bool? isActive, string? applicableCategory, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentRetentionPolicyDto>> GetActiveRetentionPoliciesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<Guid> CreateRetentionPolicyAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken = default);
    Task UpdateRetentionPolicyAsync(UpdateRetentionPolicyRequest request, CancellationToken cancellationToken = default);
    Task DeleteRetentionPolicyAsync(DeleteRetentionPolicyRequest request, CancellationToken cancellationToken = default);
    Task ActivateRetentionPolicyAsync(ActivateRetentionPolicyRequest request, CancellationToken cancellationToken = default);
    Task DeactivateRetentionPolicyAsync(DeactivateRetentionPolicyRequest request, CancellationToken cancellationToken = default);

    // ══════════════════════════════════════════════════════════════════════
    // AUDIT TRAIL
    // ══════════════════════════════════════════════════════════════════════

    Task<PagedResult<DocumentAuditTrailDto>> SearchAuditTrailAsync(Guid tenantId, Guid? documentId, Guid? workflowInstanceId, string? eventType, Guid? performedByUserId, DateTime? eventDateFrom, DateTime? eventDateTo, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentAuditTrailDto>> GetAuditTrailByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task CreateAuditTrailAsync(CreateAuditTrailRequest request, CancellationToken cancellationToken = default);

    // ══════════════════════════════════════════════════════════════════════
    // CLASSIFICATION QUEUE
    // ══════════════════════════════════════════════════════════════════════

    Task<DocumentClassificationQueueDto?> GetClassificationQueueByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<DocumentClassificationQueueDto>> SearchClassificationQueueAsync(Guid tenantId, string? queueStatus, Guid? assignedToUserId, string? priority, DateTime? dueDateFrom, DateTime? dueDateTo, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentClassificationQueueDto>> GetPendingClassificationsByUserIdAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<Guid> CreateClassificationQueueAsync(CreateClassificationQueueRequest request, CancellationToken cancellationToken = default);
    Task AssignClassificationAsync(AssignClassificationRequest request, CancellationToken cancellationToken = default);
    Task ClassifyDocumentAsync(ClassifyDocumentRequest request, CancellationToken cancellationToken = default);
    Task MarkClassificationFailedAsync(MarkClassificationFailedRequest request, CancellationToken cancellationToken = default);
    Task SkipClassificationAsync(SkipClassificationRequest request, CancellationToken cancellationToken = default);
}
