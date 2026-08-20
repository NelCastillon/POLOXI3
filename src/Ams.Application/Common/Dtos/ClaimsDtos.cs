namespace Ams.Application.Common.Dtos;

public sealed class ClaimDto
{
    public Guid ClaimId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid AccountId { get; set; }
    public string ClaimNumber { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Lob { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LossType { get; set; } = string.Empty;
    public string PrimaryClaimant { get; set; } = string.Empty;
    public DateTime DateOfLoss { get; set; }
    public DateTime DateReported { get; set; }
    public DateTime? ClosedDate { get; set; }
    public int DaysOpen { get; set; }
    public decimal TotalIncurred { get; set; }
    public decimal TotalReserves { get; set; }
    public decimal TotalPaid { get; set; }
    public string AssignedHandler { get; set; } = string.Empty;
    public bool IsLitigation { get; set; }
    public bool HasSubrogation { get; set; }
    public bool IsCatastrophe { get; set; }
    public bool IsDisputed { get; set; }
    public string FollowUpReason { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? FollowUpDueDate { get; set; }
    public bool IsSnoozed { get; set; }
    public string? CatCode { get; set; }
    public string? LossLocation { get; set; }
    public string? StateOfLoss { get; set; }
    public string? LossDescription { get; set; }
    public string? CauseOfLoss { get; set; }
    public string? CarrierClaimNumber { get; set; }
    public string? ReportedBy { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public Guid? ModifiedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class ClaimActivityDto
{
    public Guid ClaimActivityId { get; set; }
    public Guid ClaimId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Party { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public decimal? PriorAmount { get; set; }
    public DateTime ActivityDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
}

public sealed class ClaimDetailDto
{
    public ClaimDto Claim { get; set; } = new();
    public List<ClaimActivityDto> Activities { get; set; } = [];
    public List<ClaimOptionDto> Options { get; set; } = [];
    public List<ClaimAdjusterDto> Adjusters { get; set; } = [];
    public List<ClaimPartyDto> Parties { get; set; } = [];
    public List<ClaimFinancialTransactionDto> FinancialTransactions { get; set; } = [];
    public List<ClaimNoteDto> Notes { get; set; } = [];
    public List<ClaimTaskDto> Tasks { get; set; } = [];
    public List<ClaimDocumentDto> Documents { get; set; } = [];
    public List<ClaimStatusHistoryDto> StatusHistory { get; set; } = [];
}

public sealed record ClaimOptionDto(Guid ClaimOptionId, Guid TenantId, string OptionGroupCode, string OptionCode, string DisplayName, string? Description, bool IsDefault, bool IsActive, int SortOrder);
public sealed record ClaimAdjusterDto(Guid ClaimAdjusterId, Guid TenantId, Guid ClaimId, string AdjusterTypeCode, string AdjusterName, string? CompanyName, string? EmailAddress, string? PhoneNumber, string? LicenseNumber, bool IsPrimary, string AssignmentStatusCode, DateTime AssignedDateUtc, DateTime? ReleasedDateUtc);
public sealed record ClaimPartyDto(Guid ClaimPartyId, Guid TenantId, Guid ClaimId, Guid? ContactId, string PartyTypeCode, string DisplayName, string? OrganizationName, string? EmailAddress, string? PhoneNumber, string? AddressJson, string? PreferredContactMethodCode, bool IsPrimary, bool IsActive);
public sealed record ClaimFinancialTransactionDto(Guid ClaimFinancialTransactionId, Guid TenantId, Guid ClaimId, string TransactionTypeCode, DateOnly TransactionDate, decimal Amount, string CurrencyCode, string? CoverageCode, Guid? PayeeClaimPartyId, string? ReferenceNumber, string StatusCode, Guid? ReversalOfTransactionId, string? Description);
public sealed record ClaimNoteDto(Guid ClaimNoteId, Guid TenantId, Guid ClaimId, string NoteTypeCode, string Subject, string NoteText, bool IsPinned, bool IsConfidential, DateTime NoteDateUtc, string? CreatedByName);
public sealed record ClaimTaskDto(Guid ClaimTaskId, Guid TenantId, Guid ClaimId, Guid? OpsTaskItemId, string TaskTypeCode, string Title, string? Description, string PriorityCode, string StatusCode, Guid? AssignedToUserId, string? AssignedToName, DateOnly? DueDate, DateTime? CompletedDateUtc);
public sealed record ClaimDocumentDto(Guid ClaimDocumentLinkId, Guid TenantId, Guid ClaimId, Guid DocumentId, string DocumentRoleCode, string FileName, string? ContentType, long? FileSizeBytes, string StatusCode, string? Description, DateTime LinkedDateUtc);
public sealed record ClaimStatusHistoryDto(Guid ClaimStatusHistoryId, Guid TenantId, Guid ClaimId, string? OldStatusCode, string NewStatusCode, string? ReasonCode, string? Notes, DateTime ChangedDateUtc, Guid? ChangedByUserId);
public sealed record LossRunDto(Guid LossRunId, Guid TenantId, Guid AccountId, Guid? PolicyId, Guid? CarrierId, string LossRunNumber, DateOnly AsOfDate, DateOnly? PeriodStartDate, DateOnly? PeriodEndDate, Guid? SourceDocumentId, string? SourceFileName, string ImportStatusCode, int TotalClaimCount, decimal TotalIncurred, decimal TotalReserved, decimal TotalPaid, DateTime CreatedDateUtc);
public sealed record LossRunLineDto(Guid LossRunLineId, Guid TenantId, Guid LossRunId, int LineNumber, Guid? ClaimId, string? CarrierClaimNumber, string? PolicyNumber, string? ClaimantName, DateOnly? DateOfLoss, string? ClaimStatusCode, string? LossDescription, decimal IncurredAmount, decimal ReserveAmount, decimal PaidAmount, string MatchStatusCode, string? ValidationErrorsJson);
public sealed record LossRunImportResultDto(Guid LossRunId, int ImportedLineCount, int MatchedLineCount, int ValidationErrorCount, string ImportStatusCode);

public sealed class CatEventDto
{
    public Guid CatEventId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CatCode { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string AffectedStates { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Description { get; set; } = string.Empty;
}

public sealed class AffectedInsuredDto
{
    public Guid AffectedInsuredId { get; set; }
    public Guid CatEventId { get; set; }
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string Lob { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public decimal TivAtRisk { get; set; }
    public bool GeoTagged { get; set; }
    public bool FnolFiled { get; set; }
    public bool BlastSent { get; set; }
    public string ContactStatus { get; set; } = string.Empty;
    public string Handler { get; set; } = string.Empty;
}

public sealed class CatastrophePageDto
{
    public List<CatEventDto> Events { get; set; } = [];
    public List<AffectedInsuredDto> AffectedInsureds { get; set; } = [];
    public List<ClaimDto> Claims { get; set; } = [];
    public List<ClaimActivityDto> Campaigns { get; set; } = [];
}
