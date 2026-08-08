using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Ams.Application.Features.DocumentIntake;

public static class DocumentIntakeModules
{
    public const string Submission = "SUBMISSION";
    public const string Policy = "POLICY";
    public const string Lead = "LEAD";
    public const string Renewal = "RENEWAL";
    public const string Claim = "CLAIM";
    public const string BindRequest = "BIND_REQUEST";
    public const string Endorsement = "ENDORSEMENT";
    public const string Account = "ACCOUNT";
    public const string Certificate = "CERTIFICATE";
    public const string Accounting = "ACCOUNTING";
    public const string CarrierInbox = "CARRIER_INBOX";
    public const string ProducerWorkspace = "PRODUCER_WORKSPACE";
    public const string Crm = "CRM";
    public const string Task = "TASK";
    public const string Compliance = "COMPLIANCE";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Submission, Policy, Lead, Renewal, Claim, BindRequest, Endorsement, Account, Certificate,
        Accounting, CarrierInbox, ProducerWorkspace, Crm, Task, Compliance
    };
}

public static class DocumentIntakeStatuses
{
    public const string Draft = "DRAFT";
    public const string Queued = "QUEUED";
    public const string Processing = "PROCESSING";
    public const string ReviewRequired = "REVIEW_REQUIRED";
    public const string Ready = "READY";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}

public static class DocumentIntakeWorkTypes
{
    public const string Ocr = "OCR";
    public const string Classification = "CLASSIFICATION";
    public const string Extraction = "EXTRACTION";
    public const string KnowledgeMapping = "KNOWLEDGE_MAPPING";
    public const string Validation = "VALIDATION";
    public const string SearchIndexing = "SEARCH_INDEXING";
}

public static class DocumentIntakeWorkStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string RetryScheduled = "RETRY_SCHEDULED";
    public const string Failed = "FAILED";
    public const string DeadLettered = "DEAD_LETTERED";
    public const string Cancelled = "CANCELLED";
}

public static class DocumentIntakeReviewStatuses
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Corrected = "CORRECTED";
    public const string Rejected = "REJECTED";
}

public sealed record DocumentIntakeSessionDto(
    Guid IntakeSessionId,
    Guid TenantId,
    string SessionNumber,
    string ModuleCode,
    string EntryPointCode,
    string StatusCode,
    string PriorityCode,
    Guid? TargetEntityId,
    Guid? AssignedToUserId,
    decimal? OverallConfidence,
    int WarningCount,
    int ErrorCount,
    Guid? PromotedEntityId,
    DateTime? PromotedDateUtc,
    DateTime CreatedDateUtc,
    Guid? CreatedByUserId,
    byte[] RowVersion);

public sealed record DocumentIntakeDocumentDto(
    Guid IntakeSessionDocumentId,
    Guid DocumentId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string DocumentRoleCode,
    string? ContentHashSha256,
    int SequenceNumber);

public sealed record DocumentIntakeDocumentStatusDto(
    Guid DocumentId,
    Guid IntakeSessionId,
    string SessionNumber,
    string SessionStatusCode,
    string? WorkStatusCode,
    string? CurrentWorkTypeCode,
    int CompletedWorkItemCount,
    int TotalWorkItemCount,
    string? LastErrorCode,
    string? LastErrorMessage,
    DateTime SessionCreatedDateUtc);

public sealed record DocumentIntakeDraftFieldDto(
    Guid IntakeDraftFieldId,
    string EntityTypeCode,
    string EntityKey,
    string FieldPath,
    string? ExtractedValue,
    string? NormalizedValue,
    string? ReviewedValue,
    string ValueTypeCode,
    decimal? Confidence,
    Guid? SourceDocumentId,
    int? SourcePageNumber,
    string? SourceBoundingBoxJson,
    Guid? KnowledgeConceptId,
    string MappingStatusCode,
    string ReviewStatusCode,
    byte[] RowVersion);

public sealed record DocumentIntakeIssueDto(
    Guid IntakeIssueId,
    string IssueCode,
    string IssueTypeCode,
    string SeverityCode,
    string? FieldPath,
    string Message,
    string? ExistingValue,
    string? ExtractedValue,
    string StatusCode,
    Guid? ResolvedByUserId,
    DateTime? ResolvedDateUtc,
    string? ResolutionNotes,
    DateTime CreatedDateUtc,
    byte[] RowVersion);

public sealed record DocumentIntakeWorkItemDto(
    Guid IntakeWorkItemId,
    Guid IntakeSessionId,
    Guid? DocumentId,
    string WorkTypeCode,
    string StatusCode,
    int AttemptCount,
    int MaxAttempts,
    DateTime AvailableDateUtc,
    string? LeaseOwner,
    DateTime? LeaseExpiresDateUtc,
    string? LastErrorCode,
    string? LastErrorMessage,
    string CorrelationId,
    byte[] RowVersion);

public sealed record DocumentIntakeReviewHistoryDto(
    Guid IntakeReviewHistoryId,
    Guid? IntakeDraftFieldId,
    string ActionCode,
    string? PreviousValue,
    string? NewValue,
    string Reason,
    Guid ReviewedByUserId,
    string CorrelationId,
    DateTime CreatedDateUtc);

public sealed record DocumentIntakeDetailDto(
    DocumentIntakeSessionDto Session,
    IReadOnlyCollection<DocumentIntakeDocumentDto> Documents,
    IReadOnlyCollection<DocumentIntakeDraftFieldDto> DraftFields,
    IReadOnlyCollection<DocumentIntakeIssueDto> Issues,
    IReadOnlyCollection<DocumentIntakeWorkItemDto> WorkItems,
    IReadOnlyCollection<DocumentIntakeReviewHistoryDto> ReviewHistory,
    DocumentIntakePromotionReadinessDto? PromotionReadiness = null);

public sealed record DocumentIntakePromotionReadinessDto(
    bool CanPromote,
    Guid? LobId,
    string? LobCode,
    string? LobName,
    IReadOnlyCollection<string> Blockers);

public sealed record DocumentIntakePromotionConfigurationDto(
    Guid IntakePromotionConfigurationId,
    Guid TenantId,
    string ModuleCode,
    bool RequireReadyStatus,
    bool RequireCanonicalLob,
    bool LinkSourceDocuments,
    bool CreateFollowUpTask,
    string? FollowUpTaskTitle,
    string? FollowUpTaskDescription,
    int? FollowUpDueDays,
    string FollowUpTaskPriorityCode,
    string OpportunityLinePriorityCode,
    string OpportunityLineStatusCode,
    int OpportunityCloseDays,
    decimal OpportunityWinProbability,
    int SubmissionTermMonths);

public sealed record CreateDocumentIntakeSessionCommand(
    [Required] Guid TenantId,
    [Required, StringLength(200)] string IdempotencyKey,
    [Required, StringLength(50)] string ModuleCode,
    [Required, StringLength(50)] string EntryPointCode,
    [Required, StringLength(30)] string PriorityCode,
    Guid? TargetEntityId,
    Guid? AssignedToUserId,
    [Required, StringLength(120)] string CorrelationId,
    Guid? CreatedByUserId);

public sealed record AttachDocumentToIntakeCommand(
    [Required] Guid TenantId,
    [Required] Guid IntakeSessionId,
    [Required] Guid DocumentId,
    [Required, StringLength(50)] string DocumentRoleCode,
    [StringLength(64, MinimumLength = 64)] string? ContentHashSha256,
    [Range(1, int.MaxValue)] int SequenceNumber,
    Guid? ActorUserId);

public sealed record QueueDocumentIntakeCommand(
    [Required] Guid TenantId,
    [Required] Guid IntakeSessionId,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record ReviewDocumentIntakeFieldCommand(
    [Required] Guid TenantId,
    [Required] Guid IntakeSessionId,
    [Required] Guid IntakeDraftFieldId,
    [Required, StringLength(30)] string DecisionCode,
    string? ReviewedValue,
    [Required, StringLength(2000)] string Reason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ReviewerUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record ResolveDocumentIntakeIssueCommand(
    [Required] Guid TenantId,
    [Required] Guid IntakeSessionId,
    [Required] Guid IntakeIssueId,
    [Required, StringLength(30)] string ResolutionCode,
    [Required, StringLength(2000)] string ResolutionNotes,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ReviewerUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record ReprocessDocumentIntakeCommand(
    [Required] Guid TenantId,
    [Required] Guid IntakeSessionId,
    [Required, StringLength(50)] string FromWorkTypeCode,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record CancelDocumentIntakeCommand(
    [Required] Guid TenantId,
    [Required] Guid IntakeSessionId,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record PromoteDocumentIntakeCommand(
    [Required] Guid TenantId,
    [Required] Guid IntakeSessionId,
    Guid? ExistingAccountId,
    bool CreateNewAccount,
    [Required, StringLength(240)] string IdempotencyKey,
    [Required, StringLength(1000)] string ChangeReason,
    [Required, StringLength(120)] string CorrelationId,
    [Required] Guid ActorUserId,
    [Required, MinLength(8)] byte[] RowVersion);

public sealed record DocumentOcrRequest(
    Guid TenantId,
    Guid IntakeSessionId,
    Guid WorkItemId,
    Guid DocumentId,
    string FileName,
    string ContentType,
    Stream Content,
    string InputReference,
    string CorrelationId);

public sealed record DocumentOcrResult(
    string ProviderCode,
    string ModelName,
    string RawText,
    IReadOnlyCollection<DocumentOcrPage> Pages,
    decimal? Confidence,
    string OutputJson,
    long DurationMilliseconds);

public sealed record DocumentOcrPage(
    int PageNumber,
    string Text,
    IReadOnlyCollection<DocumentOcrField> Fields,
    IReadOnlyCollection<DocumentOcrTable> Tables);

public sealed record DocumentOcrField(string Name, string? Value, decimal? Confidence, string? BoundingBoxJson);
public sealed record DocumentOcrTable(int RowCount, int ColumnCount, IReadOnlyCollection<DocumentOcrTableCell> Cells);
public sealed record DocumentOcrTableCell(int RowIndex, int ColumnIndex, string? Value, decimal? Confidence, string? BoundingBoxJson);

public sealed record DocumentInterpretationRequest(
    Guid TenantId,
    Guid IntakeSessionId,
    Guid WorkItemId,
    Guid DocumentId,
    string ModuleCode,
    string PromptCode,
    string PromptVersion,
    string SystemPrompt,
    string OutputSchemaJson,
    string OcrJson,
    string CorrelationId);

public sealed record DocumentInterpretationResult(
    string ProviderCode,
    string ModelName,
    string PromptCode,
    string PromptVersion,
    DocumentClassificationOutput Classification,
    IReadOnlyCollection<ExtractedDocumentField> Fields,
    IReadOnlyCollection<ExtractedDocumentWarning> Warnings,
    string OutputJson,
    int? InputTokenCount,
    int? OutputTokenCount,
    long DurationMilliseconds);

public sealed record DocumentClassificationOutput(
    [property: JsonPropertyName("documentTypeCode")] string DocumentTypeCode,
    [property: JsonPropertyName("confidence")] decimal Confidence);

public sealed record ExtractedDocumentField(
    [property: JsonPropertyName("entityTypeCode")] string EntityTypeCode,
    [property: JsonPropertyName("entityKey")] string EntityKey,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("valueTypeCode")] string ValueTypeCode,
    [property: JsonPropertyName("confidence")] decimal Confidence,
    [property: JsonPropertyName("sourcePage")] int? SourcePage,
    [property: JsonPropertyName("boundingBoxJson")] string? BoundingBoxJson);

public sealed record ExtractedDocumentWarning(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("severityCode")] string SeverityCode,
    [property: JsonPropertyName("fieldPath")] string? FieldPath,
    [property: JsonPropertyName("message")] string Message);

public sealed record KnowledgeNormalizationRequest(
    Guid TenantId,
    Guid IntakeSessionId,
    string ModuleCode,
    IReadOnlyCollection<ExtractedDocumentField> Fields,
    string CorrelationId);

public sealed record KnowledgeNormalizedField(
    string EntityTypeCode,
    string EntityKey,
    string FieldPath,
    string? ExtractedValue,
    string? NormalizedValue,
    Guid? KnowledgeConceptId,
    string MappingStatusCode,
    decimal? Confidence);

public sealed record SubmissionIntakeDraft(
    string Source,
    string? ApplicantName,
    string BusinessName,
    string? Fein,
    string? Email,
    string? Phone,
    string? AddressLine,
    string? City,
    string? State,
    string? PostalCode,
    string? ExistingPolicyNumber,
    string? ProducerCode,
    string LineOfBusiness,
    DateTime? RequestedEffectiveDate,
    decimal? EstimatedPremium,
    string? Notes);
