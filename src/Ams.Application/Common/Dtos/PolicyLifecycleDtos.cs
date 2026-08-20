namespace Ams.Application.Common.Dtos;

public sealed class PolicyLifecycleOptionDto
{
    public Guid PolicyLifecycleOptionId { get; set; }
    public Guid TenantId { get; set; }
    public string OptionGroupCode { get; set; } = string.Empty;
    public string OptionCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsTerminal { get; set; }
    public bool IsPremiumBearing { get; set; }
    public bool RequiresDocument { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PolicyTransactionTransitionDto
{
    public Guid PolicyTransactionTransitionId { get; set; }
    public Guid TenantId { get; set; }
    public string? TransactionTypeCode { get; set; }
    public string FromStatusCode { get; set; } = string.Empty;
    public string ToStatusCode { get; set; } = string.Empty;
    public bool RequiresDocument { get; set; }
    public bool RequiresApproval { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PolicyLifecycleWorkbenchRowDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid? PolicyTermId { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public string Mode { get; set; } = "policies";
    public string Number { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public int DocumentCount { get; set; }
    public int VersionNumber { get; set; }
    public string? NextStatusCode { get; set; }
    public string? NextStatusDisplayName { get; set; }
    public bool NextTransitionRequiresDocument { get; set; }
    public bool NextTransitionRequiresApproval { get; set; }
}

public sealed class PolicyLifecyclePolicySummaryDto
{
    public Guid PolicyId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public decimal AnnualPremium { get; set; }
}

public sealed class PolicyTransactionDto
{
    public Guid PolicyTransactionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid? PolicyTermId { get; set; }
    public Guid? ParentPolicyTransactionId { get; set; }
    public Guid? SupersedesPolicyTransactionId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public string TransactionTypeCode { get; set; } = string.Empty;
    public string TransactionStatusCode { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime? RequestedDateUtc { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public DateTime? IssuedDateUtc { get; set; }
    public DateTime? ProcessedDateUtc { get; set; }
    public decimal? PriorWrittenPremium { get; set; }
    public decimal? PremiumChange { get; set; }
    public decimal? NewWrittenPremium { get; set; }
    public decimal? TaxesChange { get; set; }
    public decimal? FeesChange { get; set; }
    public decimal? SurchargesChange { get; set; }
    public decimal? TotalCostChange { get; set; }
    public string? ReasonCode { get; set; }
    public string? SourceCode { get; set; }
    public string? ExternalReference { get; set; }
    public string? CarrierTransactionNumber { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public Guid? IssuedByUserId { get; set; }
    public int CurrentVersionNumber { get; set; }
    public int DocumentCount { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class PolicyTransactionLineChangeDto
{
    public Guid PolicyTransactionLineChangeId { get; set; }
    public Guid PolicyTransactionId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid? PolicyTermId { get; set; }
    public Guid? PolicyLineId { get; set; }
    public Guid? LineOfBusinessId { get; set; }
    public string LineOfBusinessCode { get; set; } = string.Empty;
    public string LineOfBusinessName { get; set; } = string.Empty;
    public string ChangeTypeCode { get; set; } = string.Empty;
    public decimal? PriorPremium { get; set; }
    public decimal? PremiumChange { get; set; }
    public decimal? NewPremium { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}

public sealed class PolicyTransactionDocumentDto
{
    public Guid PolicyTransactionDocumentId { get; set; }
    public Guid PolicyTransactionId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid? DocumentId { get; set; }
    public string DocumentRoleCode { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public string? FileName { get; set; }
    public string? StorageUri { get; set; }
    public DateTime LinkedDateUtc { get; set; }
}

public sealed class PolicyTermHistoryDto
{
    public Guid PolicyTermHistoryId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid PolicyTermId { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public int TermNumber { get; set; }
    public string TermStatusCode { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public DateTime ExpirationDate { get; set; }
    public decimal? WrittenPremium { get; set; }
    public decimal? AnnualizedPremium { get; set; }
    public decimal? TotalCost { get; set; }
    public string? SnapshotJson { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class PolicyVersionDto
{
    public Guid PolicyVersionId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid? PolicyTermId { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public int VersionNumber { get; set; }
    public string VersionReasonCode { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = "{}";
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class PolicyStatusHistoryDto
{
    public Guid PolicyStatusHistoryId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid? PolicyTermId { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public string StatusScopeCode { get; set; } = string.Empty;
    public string? OldStatusCode { get; set; }
    public string NewStatusCode { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public string? Notes { get; set; }
    public DateTime ChangedDateUtc { get; set; }
    public Guid? ChangedByUserId { get; set; }
}

public sealed class PolicyLifecycleDetailDto
{
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public PolicyLifecyclePolicySummaryDto Policy { get; set; } = new();
    public IReadOnlyList<PolicyTransactionDto> Transactions { get; set; } = [];
    public IReadOnlyList<PolicyTransactionLineChangeDto> LineChanges { get; set; } = [];
    public IReadOnlyList<PolicyTransactionDocumentDto> Documents { get; set; } = [];
    public IReadOnlyList<PolicyTermHistoryDto> TermHistory { get; set; } = [];
    public IReadOnlyList<PolicyVersionDto> Versions { get; set; } = [];
    public IReadOnlyList<PolicyStatusHistoryDto> StatusHistory { get; set; } = [];
    public IReadOnlyList<PolicyTransactionTransitionDto> Transitions { get; set; } = [];
}

public sealed class PolicyServicingWorkspaceDto
{
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public PolicyLifecyclePolicySummaryDto Policy { get; set; } = new();
    public IReadOnlyList<PolicyLifecycleOptionDto> Options { get; set; } = [];
    public IReadOnlyList<PolicyTransactionDto> Transactions { get; set; } = [];
    public IReadOnlyList<PolicyVersionDto> Versions { get; set; } = [];
    public IReadOnlyList<PolicyStatusHistoryDto> StatusHistory { get; set; } = [];
    public IReadOnlyList<PolicyServicingActivityDto> Activities { get; set; } = [];
    public IReadOnlyList<PolicyServicingCommunicationDto> Communications { get; set; } = [];
    public IReadOnlyList<PolicyServicingTaskDto> Tasks { get; set; } = [];
    public IReadOnlyList<PolicyServicingComplianceDocumentDto> ComplianceDocuments { get; set; } = [];
    public IReadOnlyList<PolicyServicingTimelineEntryDto> Timeline { get; set; } = [];
}

public sealed class PolicyServicingActivityDto
{
    public Guid ActivityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public DateTime ActivityDateUtc { get; set; }
    public string ActivityTypeCode { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ChannelCode { get; set; }
    public string? OutcomeCode { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public Guid? PerformedByUserId { get; set; }
    public string? PerformedByName { get; set; }
}

public sealed class PolicyServicingCommunicationDto
{
    public Guid ThreadId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public string ChannelCode { get; set; } = string.Empty;
    public string DirectionCode { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Recipient { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public int MessageCount { get; set; }
    public DateTime LastActivityDateUtc { get; set; }
}

public sealed class PolicyServicingTaskDto
{
    public Guid TaskId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public string TaskTypeCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public DateTime? DueDateUtc { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
}

public sealed class PolicyServicingComplianceDocumentDto
{
    public Guid PolicyDocumentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public string PolicyCode { get; set; } = string.Empty;
    public string PolicyTitle { get; set; } = string.Empty;
    public string PolicyTypeCode { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime? EffectiveDateUtc { get; set; }
    public bool IsActive { get; set; }
}

public sealed class PolicyServicingTimelineEntryDto
{
    public Guid EntryId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public string EntryTypeCode { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? StatusCode { get; set; }
    public DateTime OccurredDateUtc { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorName { get; set; }
}

public sealed class PolicyServicingActionResultDto
{
    public Guid PolicyId { get; set; }
    public Guid RecordId { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public string RecordTypeCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
