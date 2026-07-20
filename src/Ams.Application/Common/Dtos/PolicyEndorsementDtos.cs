namespace Ams.Application.Common.Dtos;

public sealed class PolicyEndorsementDto
{
    public Guid EndorsementId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? AccountId { get; set; }
    public string EndorsementNumber { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string EndorsementType { get; set; } = string.Empty;
    public string? RequestSourceCode { get; set; }
    public string? ChangeCategoryCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime? RetroactiveDate { get; set; }
    public DateTime? DiscoveryDate { get; set; }
    public DateTime RequestedDateUtc { get; set; }
    public decimal PremiumDelta { get; set; }
    public decimal TaxFeeDelta { get; set; }
    public decimal TotalCostDelta { get; set; }
    public decimal ProratedPremiumDelta { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public string? RequestedByEmail { get; set; }
    public string? RequestedByPhone { get; set; }
    public string? ClientContactName { get; set; }
    public string? ClientContactEmail { get; set; }
    public string? ClientContactPhone { get; set; }
    public string AssignedToName { get; set; } = string.Empty;
    public string? UnderwriterName { get; set; }
    public string? UnderwriterEmail { get; set; }
    public DateTime? CarrierSubmissionDateUtc { get; set; }
    public DateTime? CarrierResponseDueDate { get; set; }
    public string? CarrierReferenceNumber { get; set; }
    public bool BrokerOfRecordRequired { get; set; }
    public string? AgentAuthorityCode { get; set; }
    public string? ApprovalLevelCode { get; set; }
    public string? ApprovedByName { get; set; }
    public string? IssuedByName { get; set; }
    public string? BillingImpactCode { get; set; }
    public string? CommissionImpactCode { get; set; }
    public string? BillingInstruction { get; set; }
    public string? DocumentDeliveryCode { get; set; }
    public bool CertificateRequired { get; set; }
    public string? FormsRequired { get; set; }
    public string? AcordFormNumbers { get; set; }
    public string? ExternalReferenceNumber { get; set; }
    public bool ComplianceReviewRequired { get; set; }
    public string? EoExposureNotes { get; set; }
    public string? InternalNotes { get; set; }
    public string? ClientFacingNotes { get; set; }
    public string? Reason { get; set; }
    public string? RequiredDocuments { get; set; }
    public string? WorkflowStage { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public DateTime? IssuedDateUtc { get; set; }
    public bool IsUrgent { get; set; }
    public bool IsArchived { get; set; }
    public int DaysOpen => Math.Max(0, (DateTime.UtcNow.Date - RequestedDateUtc.Date).Days);
}

public sealed class PolicyEndorsementActivityDto
{
    public Guid ActivityId { get; set; }
    public Guid EndorsementId { get; set; }
    public Guid TenantId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime ActivityDateUtc { get; set; }
}

public sealed class PolicyEndorsementDeltaDto
{
    public Guid DeltaId { get; set; }
    public Guid EndorsementId { get; set; }
    public Guid TenantId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string BeforeValue { get; set; } = string.Empty;
    public string AfterValue { get; set; } = string.Empty;
    public decimal NumericDelta { get; set; }
}

public sealed class PolicyEndorsementCenterDto
{
    public IReadOnlyList<PolicyEndorsementDto> Endorsements { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementActivityDto> Activities { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementDeltaDto> Deltas { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementOptionDto> Options { get; set; } = [];
}

public sealed class PolicyEndorsementOptionDto
{
    public Guid OptionId { get; set; }
    public Guid TenantId { get; set; }
    public string OptionGroupCode { get; set; } = string.Empty;
    public string OptionCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PolicyEndorsementDetailDto
{
    public PolicyEndorsementDto Endorsement { get; set; } = new();
    public IReadOnlyList<PolicyEndorsementActivityDto> Activities { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementDeltaDto> Deltas { get; set; } = [];
}
