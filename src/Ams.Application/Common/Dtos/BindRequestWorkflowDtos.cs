namespace Ams.Application.Common.Dtos;

public sealed class BindRequestDetailDto
{
    public PolicyBindTransactionDto Request { get; set; } = new();
    public IReadOnlyList<BindRequirementDto> Requirements { get; set; } = [];
    public IReadOnlyList<BindValidationResultDto> Validations { get; set; } = [];
    public IReadOnlyList<BindStatusHistoryDto> StatusHistory { get; set; } = [];
    public IReadOnlyList<BindApprovalDto> Approvals { get; set; } = [];
    public IReadOnlyList<BindDocumentDto> Documents { get; set; } = [];
    public IReadOnlyList<BindCarrierMessageDto> CarrierMessages { get; set; } = [];
    public IReadOnlyList<BindPackageDto> Packages { get; set; } = [];
    public BinderReviewDto? BinderReview { get; set; }
    public PolicyGenerationRequestDto? PolicyGeneration { get; set; }
    public IReadOnlyList<BindStatusTransitionDto> AllowedTransitions { get; set; } = [];
    public IReadOnlyList<SubmissionReferenceOptionDto> BindingMethods { get; set; } = [];
    public IReadOnlyList<SubmissionReferenceOptionDto> BindingAuthorities { get; set; } = [];
    public IReadOnlyList<SubmissionReferenceOptionDto> ApprovalReasons { get; set; } = [];
    public bool IsReadyToSubmit { get; set; }
    public int BlockingValidationCount { get; set; }
}

public sealed class BinderReviewDto
{
    public Guid BinderReviewId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyBindTransactionId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? PolicyNumber { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal Premium { get; set; }
    public decimal? Fees { get; set; }
    public decimal? Taxes { get; set; }
    public decimal? CommissionPercent { get; set; }
    public string? PaymentPlan { get; set; }
    public string? BillingTypeCode { get; set; }
    public Guid? ProducerId { get; set; }
    public Guid? CsrId { get; set; }
    public string CoverageSnapshotJson { get; set; } = "{}";
    public string RiskSnapshotJson { get; set; } = "{}";
    public string ComparisonSnapshotJson { get; set; } = "{}";
    public string? ReviewNotes { get; set; }
    public DateTime? ReviewedDateUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? AcceptedDateUtc { get; set; }
    public Guid? AcceptedByUserId { get; set; }
}

public sealed class PolicyGenerationRequestDto
{
    public Guid PolicyGenerationRequestId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyBindTransactionId { get; set; }
    public Guid BinderReviewId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime RequestedDateUtc { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorDetails { get; set; }
    public Guid? PolicyId { get; set; }
}

public sealed class BindStatusTransitionDto
{
    public string FromStatusCode { get; set; } = string.Empty;
    public string ToStatusCode { get; set; } = string.Empty;
    public bool RequiresValidation { get; set; }
    public bool RequiresApproval { get; set; }
    public bool RequiresCarrierResponse { get; set; }
}

public sealed class BindPackageDto
{
    public Guid BindPackageId { get; set; }
    public Guid PolicyBindTransactionId { get; set; }
    public string PackageNumber { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime PreparedDateUtc { get; set; }
    public Guid? PreparedByUserId { get; set; }
    public int DocumentCount { get; set; }
    public string? Notes { get; set; }
}

public sealed class BindRequirementDto
{
    public Guid BindRequirementId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CarrierId { get; set; }
    public string? LineOfBusiness { get; set; }
    public string RequirementCode { get; set; } = string.Empty;
    public string RequirementName { get; set; } = string.Empty;
    public string RequirementTypeCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DocumentCategoryCode { get; set; }
    public bool IsRequired { get; set; }
    public bool BlocksSubmission { get; set; }
    public int SortOrder { get; set; }
}

public sealed class BindValidationResultDto
{
    public Guid BindValidationResultId { get; set; }
    public Guid PolicyBindTransactionId { get; set; }
    public Guid? BindRequirementId { get; set; }
    public string RequirementCode { get; set; } = string.Empty;
    public string RequirementName { get; set; } = string.Empty;
    public string RequirementTypeCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public bool IsBlocking { get; set; }
    public string? Message { get; set; }
    public Guid? EvidenceDocumentId { get; set; }
    public DateTime ValidatedDateUtc { get; set; }
    public Guid? ValidatedByUserId { get; set; }
}

public sealed class BindStatusHistoryDto
{
    public Guid BindStatusHistoryId { get; set; }
    public Guid PolicyBindTransactionId { get; set; }
    public string? OldStatusCode { get; set; }
    public string NewStatusCode { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceInfo { get; set; }
    public DateTime ChangedDateUtc { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
}

public sealed class BindApprovalDto
{
    public Guid BindApprovalId { get; set; }
    public Guid PolicyBindTransactionId { get; set; }
    public string ApprovalReasonCode { get; set; } = string.Empty;
    public string? ApprovalReasonName { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public Guid? RequestedByUserId { get; set; }
    public DateTime RequestedDateUtc { get; set; }
    public Guid? AssignedApproverUserId { get; set; }
    public Guid? DecisionByUserId { get; set; }
    public DateTime? DecisionDateUtc { get; set; }
    public string? DecisionNotes { get; set; }
}

public sealed class BindDocumentDto
{
    public Guid BindDocumentId { get; set; }
    public Guid PolicyBindTransactionId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentRoleCode { get; set; } = string.Empty;
    public bool IsRequiredEvidence { get; set; }
    public string? FileName { get; set; }
    public string? Category { get; set; }
}

public sealed class BindCarrierMessageDto
{
    public Guid BindCarrierMessageId { get; set; }
    public Guid PolicyBindTransactionId { get; set; }
    public string DirectionCode { get; set; } = string.Empty;
    public string MessageTypeCode { get; set; } = string.Empty;
    public string? DeliveryMethodCode { get; set; }
    public string? ExternalMessageId { get; set; }
    public string? Subject { get; set; }
    public string? MessageBody { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime SentReceivedDateUtc { get; set; }
}

public sealed class BindCarrierResponseResult
{
    public Guid? PolicyId { get; set; }
}
