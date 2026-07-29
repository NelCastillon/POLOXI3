namespace Ams.Application.Common.Dtos;

public sealed class SubmissionDto
{
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public Guid OpportunityId { get; set; }
    public string OpportunityName { get; set; } = string.Empty;
    public string SubmissionNumber { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public decimal? TargetPremium { get; set; }
    public int MarketCount { get; set; }
    public int QuoteCount { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class SubmissionActivityDto
{
    public Guid ActivityId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class SubmissionLineDto
{
    public Guid SubmissionLineId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid? OpportunityId { get; set; }
    public Guid? OpportunityLineId { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public string? Carrier { get; set; }
    public decimal TargetPremium { get; set; }
    public string? Priority { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime? TargetEffectiveDate { get; set; }
}

public sealed class SubmissionIntakeQuestionDto
{
    public Guid IntakeQuestionId { get; set; }
    public Guid? ReadinessRequirementId { get; set; }
    public Guid? SubmissionMarketId { get; set; }
    public Guid? CarrierId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public string QuestionCode { get; set; } = string.Empty;
    public string RequirementTypeCode { get; set; } = string.Empty;
    public string ScopeCode { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string HelpText { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool BlocksSubmit { get; set; }
    public bool AllowsWaiver { get; set; }
    public bool RequiresEvidence { get; set; }
    public string? EvidencePrompt { get; set; }
    public string? ApprovalRoleCode { get; set; }
    public string? AnswerText { get; set; }
    public bool IsAnswered { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? StatusReason { get; set; }
    public Guid? EvidenceDocumentId { get; set; }
    public IReadOnlyList<SubmissionReadinessEvidenceDocumentDto> EvidenceDocuments { get; set; } = [];
    public string? WaiverReason { get; set; }
    public Guid? WaivedByUserId { get; set; }
    public DateTime? WaivedDateUtc { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public DateTime? ReviewDueDateUtc { get; set; }
    public int ScoreWeight { get; set; }
    public int SortOrder { get; set; }
    public Guid? AnsweredByUserId { get; set; }
    public DateTime? AnsweredDateUtc { get; set; }
}

public sealed class SubmissionReadinessEvidenceDocumentDto
{
    public Guid SubmissionReadinessEvidenceDocumentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid IntakeQuestionId { get; set; }
    public Guid? ReadinessRequirementId { get; set; }
    public Guid? SubmissionMarketId { get; set; }
    public Guid? CarrierId { get; set; }
    public Guid DocumentId { get; set; }
    public string EvidenceRoleCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string DocumentTypeCode { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime DocumentCreatedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class SubmissionDocumentChecklistDto
{
    public Guid ChecklistItemId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid TenantId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsSatisfied { get; set; }
    public Guid? DocumentId { get; set; }
    public string? FileName { get; set; }
    public DateTime? UploadedDateUtc { get; set; }
}

public sealed class SubmissionReadinessDto
{
    public Guid SubmissionId { get; set; }
    public Guid? SubmissionMarketId { get; set; }
    public Guid? CarrierId { get; set; }
    public string? CarrierName { get; set; }
    public int ReadinessScore { get; set; }
    public int RequiredQuestionCount { get; set; }
    public int AnsweredRequiredQuestionCount { get; set; }
    public int WaivedRequiredQuestionCount { get; set; }
    public int RequiredQuestionScoreWeight { get; set; }
    public int SatisfiedQuestionScoreWeight { get; set; }
    public int RequiredDocumentCount { get; set; }
    public int SatisfiedRequiredDocumentCount { get; set; }
    public bool IsReadyForMarketing { get; set; }
    public IReadOnlyList<string> BlockingReasons { get; set; } = [];
}

public sealed class SubmissionReadinessRequirementDto
{
    public Guid ReadinessRequirementId { get; set; }
    public Guid TenantId { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public Guid? CarrierId { get; set; }
    public string? CarrierName { get; set; }
    public string? StateCode { get; set; }
    public string? ChannelCode { get; set; }
    public string ScopeCode { get; set; } = string.Empty;
    public string RequirementCode { get; set; } = string.Empty;
    public string RequirementTypeCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public bool BlocksSubmit { get; set; }
    public bool AllowsWaiver { get; set; }
    public bool RequiresEvidence { get; set; }
    public string? EvidencePrompt { get; set; }
    public string? ApprovalRoleCode { get; set; }
    public string? ActionCode { get; set; }
    public string? ActionLabel { get; set; }
    public int ScoreWeight { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class SubmissionPackagePreviewDto
{
    public Guid SubmissionId { get; set; }
    public Guid? SubmissionMarketId { get; set; }
    public Guid? CarrierId { get; set; }
    public string? CarrierName { get; set; }
    public string ChannelCode { get; set; } = string.Empty;
    public string ChannelDescription { get; set; } = string.Empty;
    public string PackageStatus { get; set; } = string.Empty;
    public string SubmissionNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string? RequestedCoverageSummary { get; set; }
    public string? RequestedLimits { get; set; }
    public decimal? RequestedPremium { get; set; }
    public IReadOnlyList<SubmissionPackagePreviewLineDto> Lines { get; set; } = [];
    public IReadOnlyList<SubmissionPackagePreviewDocumentDto> Documents { get; set; } = [];
    public IReadOnlyList<SubmissionPackagePreviewReadinessDto> ReadinessItems { get; set; } = [];
    public IReadOnlyList<CarrierTransmissionDto> Transmissions { get; set; } = [];
}

public sealed class SubmissionPackagePreviewLineDto
{
    public Guid SubmissionLineId { get; set; }
    public string LineOfBusiness { get; set; } = string.Empty;
    public decimal TargetPremium { get; set; }
}

public sealed class SubmissionPackagePreviewDocumentDto
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string DocumentTypeCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class SubmissionPackagePreviewReadinessDto
{
    public Guid IntakeQuestionId { get; set; }
    public string RequirementCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool BlocksSubmit { get; set; }
    public bool RequiresEvidence { get; set; }
    public Guid? EvidenceDocumentId { get; set; }
    public string? EvidenceFileName { get; set; }
    public string? EvidenceCategoryCode { get; set; }
    public string? EvidenceDocumentTypeCode { get; set; }
    public IReadOnlyList<SubmissionReadinessEvidenceDocumentDto> EvidenceDocuments { get; set; } = [];
}

public sealed class SubmissionTaskTemplateDto
{
    public string TaskTypeCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = "Medium";
    public int DefaultDueDays { get; set; }
}

public sealed class SubmissionMetricsDto
{
    public int PendingIntake { get; set; }
    public int ReadyForMarket { get; set; }
    public int MarketsAwaitingResponse { get; set; }
    public int QuotesExpiringSoon { get; set; }
    public int ProposalsPendingDecision { get; set; }
    public int BindRequestsPending { get; set; }
}

public sealed class SubmissionTaskDto
{
    public Guid TaskItemId { get; set; }
    public Guid TenantId { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TaskTypeCode { get; set; } = string.Empty;
    public string StageCode { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
