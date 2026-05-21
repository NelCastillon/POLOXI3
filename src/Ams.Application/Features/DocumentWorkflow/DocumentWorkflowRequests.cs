namespace Ams.Application.Features.DocumentWorkflow;

// ══════════════════════════════════════════════════════════════════════
// WORKFLOW TEMPLATE REQUESTS
// ══════════════════════════════════════════════════════════════════════

public sealed record CreateWorkflowTemplateRequest(
    Guid TenantId,
    string TemplateName,
    string TemplateCode,
    string WorkflowType,
    string? Description,
    bool IsSequential,
    bool RequiresAllApprovals,
    bool AutoArchiveOnComplete,
    bool NotifyOnStart,
    bool NotifyOnComplete,
    bool TriggerOnUpload,
    string? TriggerOnCategory,
    string? TriggerOnDocType,
    int SortOrder,
    Guid? CreatedByUserId
);

public sealed record UpdateWorkflowTemplateRequest(
    Guid WorkflowTemplateId,
    string TemplateName,
    string? Description,
    bool IsSequential,
    bool RequiresAllApprovals,
    bool AutoArchiveOnComplete,
    bool NotifyOnStart,
    bool NotifyOnComplete,
    bool TriggerOnUpload,
    string? TriggerOnCategory,
    string? TriggerOnDocType,
    int SortOrder,
    Guid? ModifiedByUserId
);

public sealed record DeleteWorkflowTemplateRequest(
    Guid WorkflowTemplateId,
    Guid? ModifiedByUserId
);

public sealed record ActivateWorkflowTemplateRequest(
    Guid WorkflowTemplateId,
    Guid? ModifiedByUserId
);

public sealed record DeactivateWorkflowTemplateRequest(
    Guid WorkflowTemplateId,
    Guid? ModifiedByUserId
);

// ══════════════════════════════════════════════════════════════════════
// WORKFLOW STEP TEMPLATE REQUESTS
// ══════════════════════════════════════════════════════════════════════

public sealed record CreateWorkflowStepTemplateRequest(
    Guid TenantId,
    Guid WorkflowTemplateId,
    string StepName,
    string StepType,
    int StepOrder,
    string? Description,
    string? AssignedToRoleCode,
    Guid? AssignedToUserId,
    bool AssignToBranchAdmin,
    bool AssignToDocOwner,
    bool IsRequired,
    int? DueDays,
    int? EscalateDays,
    string? EscalateToRoleCode,
    bool RequiresPreviousApproval,
    string? SkipIfCondition,
    Guid? CreatedByUserId
);

public sealed record UpdateWorkflowStepTemplateRequest(
    Guid StepTemplateId,
    string StepName,
    string? Description,
    string? AssignedToRoleCode,
    Guid? AssignedToUserId,
    bool AssignToBranchAdmin,
    bool AssignToDocOwner,
    bool IsRequired,
    int? DueDays,
    int? EscalateDays,
    string? EscalateToRoleCode,
    bool RequiresPreviousApproval,
    string? SkipIfCondition,
    Guid? ModifiedByUserId
);

public sealed record DeleteWorkflowStepTemplateRequest(
    Guid StepTemplateId,
    Guid? ModifiedByUserId
);

// ══════════════════════════════════════════════════════════════════════
// WORKFLOW INSTANCE REQUESTS
// ══════════════════════════════════════════════════════════════════════

public sealed record CreateWorkflowInstanceRequest(
    Guid TenantId,
    Guid DocumentId,
    Guid WorkflowTemplateId,
    string InstanceName,
    Guid InitiatedByUserId,
    string? InitiatedByName,
    string? Comments,
    string Priority,
    DateTime? DueDateUtc,
    Guid? CreatedByUserId
);

public sealed record StartWorkflowInstanceRequest(
    Guid WorkflowInstanceId,
    int FirstStepOrder,
    Guid? ModifiedByUserId
);

public sealed record AdvanceWorkflowInstanceRequest(
    Guid WorkflowInstanceId,
    int NextStepOrder,
    Guid? ModifiedByUserId
);

public sealed record CompleteWorkflowInstanceRequest(
    Guid WorkflowInstanceId,
    string FinalOutcome,
    string? FinalComments,
    Guid CompletedByUserId,
    string? CompletedByName,
    Guid? ModifiedByUserId
);

public sealed record RejectWorkflowInstanceRequest(
    Guid WorkflowInstanceId,
    string? FinalComments,
    Guid CompletedByUserId,
    string? CompletedByName,
    Guid? ModifiedByUserId
);

public sealed record CancelWorkflowInstanceRequest(
    Guid WorkflowInstanceId,
    string? Reason,
    Guid? ModifiedByUserId
);

public sealed record EscalateWorkflowInstanceRequest(
    Guid WorkflowInstanceId,
    Guid? ModifiedByUserId
);

// ══════════════════════════════════════════════════════════════════════
// APPROVAL REQUESTS
// ══════════════════════════════════════════════════════════════════════

public sealed record CreateApprovalRequest(
    Guid TenantId,
    Guid WorkflowInstanceId,
    Guid DocumentId,
    Guid? StepTemplateId,
    string ApprovalName,
    string ApprovalType,
    int StepOrder,
    Guid AssignedToUserId,
    string? AssignedToName,
    string? AssignedToRoleCode,
    DateTime? DueDateUtc,
    Guid? CreatedByUserId
);

public sealed record ApproveDocumentRequest(
    Guid ApprovalId,
    Guid ResponseByUserId,
    string? ResponseByName,
    string? Comments,
    Guid? ModifiedByUserId
);

public sealed record RejectDocumentRequest(
    Guid ApprovalId,
    Guid ResponseByUserId,
    string? ResponseByName,
    string? Comments,
    Guid? ModifiedByUserId
);

public sealed record DeferApprovalRequest(
    Guid ApprovalId,
    string? Comments,
    Guid? ModifiedByUserId
);

public sealed record EscalateApprovalRequest(
    Guid ApprovalId,
    Guid EscalatedToUserId,
    Guid? ModifiedByUserId
);

public sealed record ReassignApprovalRequest(
    Guid ApprovalId,
    Guid NewAssignedToUserId,
    string? NewAssignedToName,
    Guid? ModifiedByUserId
);

// ══════════════════════════════════════════════════════════════════════
// REVIEW REQUESTS
// ══════════════════════════════════════════════════════════════════════

public sealed record CreateReviewRequest(
    Guid TenantId,
    Guid DocumentId,
    Guid? WorkflowInstanceId,
    string ReviewName,
    string ReviewType,
    string? ReviewPurpose,
    Guid AssignedToUserId,
    string? AssignedToName,
    DateTime? DueDateUtc,
    Guid? CreatedByUserId
);

public sealed record StartReviewRequest(
    Guid ReviewId,
    Guid? ModifiedByUserId
);

public sealed record CompleteReviewRequest(
    Guid ReviewId,
    Guid CompletedByUserId,
    string? CompletedByName,
    string? ReviewNotes,
    int? Rating,
    int IssuesFound,
    bool RecommendChanges,
    string? ChangesDescription,
    Guid? ModifiedByUserId
);

public sealed record ReturnReviewRequest(
    Guid ReviewId,
    string? ReviewNotes,
    Guid? ModifiedByUserId
);

public sealed record CancelReviewRequest(
    Guid ReviewId,
    string? Reason,
    Guid? ModifiedByUserId
);

public sealed record ReassignReviewRequest(
    Guid ReviewId,
    Guid NewAssignedToUserId,
    string? NewAssignedToName,
    Guid? ModifiedByUserId
);

// ══════════════════════════════════════════════════════════════════════
// RETENTION POLICY REQUESTS
// ══════════════════════════════════════════════════════════════════════

public sealed record CreateRetentionPolicyRequest(
    Guid TenantId,
    string PolicyName,
    string PolicyCode,
    string? Description,
    string? ApplicableCategory,
    string? ApplicableDocType,
    string? ApplicableEntityType,
    int RetentionPeriodYears,
    string RetentionStartTrigger,
    string ActionOnExpiry,
    bool RequireApprovalToDelete,
    int? NotifyBeforeDays,
    string? NotifyRoleCode,
    string? RegulatoryBasis,
    string? ComplianceNotes,
    DateOnly EffectiveDate,
    DateOnly? ExpiryDate,
    Guid? CreatedByUserId
);

public sealed record UpdateRetentionPolicyRequest(
    Guid RetentionPolicyId,
    string PolicyName,
    string? Description,
    string? ApplicableCategory,
    string? ApplicableDocType,
    string? ApplicableEntityType,
    int RetentionPeriodYears,
    string RetentionStartTrigger,
    string ActionOnExpiry,
    bool RequireApprovalToDelete,
    int? NotifyBeforeDays,
    string? NotifyRoleCode,
    string? RegulatoryBasis,
    string? ComplianceNotes,
    DateOnly? ExpiryDate,
    Guid? ModifiedByUserId
);

public sealed record DeleteRetentionPolicyRequest(
    Guid RetentionPolicyId,
    Guid? ModifiedByUserId
);

public sealed record ActivateRetentionPolicyRequest(
    Guid RetentionPolicyId,
    Guid? ModifiedByUserId
);

public sealed record DeactivateRetentionPolicyRequest(
    Guid RetentionPolicyId,
    Guid? ModifiedByUserId
);

// ══════════════════════════════════════════════════════════════════════
// AUDIT TRAIL REQUESTS
// ══════════════════════════════════════════════════════════════════════

public sealed record CreateAuditTrailRequest(
    Guid TenantId,
    Guid DocumentId,
    Guid? WorkflowInstanceId,
    string EventType,
    string EventCategory,
    string? EventDescription,
    Guid? PerformedByUserId,
    string? PerformedByName,
    string? PerformedByRoleCode,
    string? OldValue,
    string? NewValue,
    string? ChangesSummary,
    string? IpAddress,
    string? UserAgent,
    string? SessionId,
    int RetentionYears
);

// ══════════════════════════════════════════════════════════════════════
// CLASSIFICATION QUEUE REQUESTS
// ══════════════════════════════════════════════════════════════════════

public sealed record CreateClassificationQueueRequest(
    Guid TenantId,
    Guid DocumentId,
    string ClassificationMethod,
    decimal? OcrConfidence,
    string? SuggestedCategory,
    string? SuggestedDocType,
    string? ExtractedText,
    string? ExtractedMetadata,
    string Priority,
    DateTime? DueDateUtc,
    Guid? CreatedByUserId
);

public sealed record AssignClassificationRequest(
    Guid ClassificationQueueId,
    Guid AssignedToUserId,
    string? AssignedToName,
    Guid? ModifiedByUserId
);

public sealed record ClassifyDocumentRequest(
    Guid ClassificationQueueId,
    Guid ClassifiedByUserId,
    string? ClassifiedByName,
    string FinalCategory,
    string FinalDocType,
    string? ClassificationNotes,
    Guid? ModifiedByUserId
);

public sealed record MarkClassificationFailedRequest(
    Guid ClassificationQueueId,
    string? Reason,
    Guid? ModifiedByUserId
);

public sealed record SkipClassificationRequest(
    Guid ClassificationQueueId,
    string? Reason,
    Guid? ModifiedByUserId
);
