namespace Ams.Application.Common.Dtos;

public sealed class PolicyEndorsementWorkflowDetailDto
{
    public PolicyEndorsementDto Endorsement { get; set; } = new();
    public PolicyEndorsementFinancialImpactDto FinancialImpact { get; set; } = new();
    public IReadOnlyList<PolicyEndorsementChangeDto> Changes { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementApprovalDto> Approvals { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementEventDto> Timeline { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementTransitionDto> AvailableTransitions { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementCarrierDispatchDto> CarrierDispatches { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementAccountingWorkDto> AccountingWork { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementDocumentWorkDto> DocumentWork { get; set; } = [];
    public IReadOnlyList<PolicyVersionDto> Versions { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementActivityDto> Activities { get; set; } = [];
}

public sealed class PolicyEndorsementPolicyWorkspaceDto
{
    public Guid TenantId { get; set; }
    public Guid PolicyId { get; set; }
    public PolicyLifecyclePolicySummaryDto Policy { get; set; } = new();
    public PolicyVersionDto? CurrentVersion { get; set; }
    public IReadOnlyList<PolicyEndorsementDto> Endorsements { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementEventDto> Timeline { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementOptionDto> Options { get; set; } = [];
}

public sealed class PolicyEndorsementFinancialImpactDto
{
    public string CurrencyCode { get; set; } = "USD";
    public decimal PremiumChange { get; set; }
    public decimal AgencyFee { get; set; }
    public decimal Taxes { get; set; }
    public decimal TotalDue { get; set; }
    public decimal ProratedPremiumChange { get; set; }
    public string? BillingImpactCode { get; set; }
    public string? CommissionImpactCode { get; set; }
}

public sealed class PolicyEndorsementChangeDto
{
    public Guid ChangeId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EndorsementId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string OperationCode { get; set; } = string.Empty;
    public string? EntityKey { get; set; }
    public int SequenceNumber { get; set; }
    public string? Summary { get; set; }
    public PolicyEndorsementInsuredChangeDto? Insured { get; set; }
    public PolicyEndorsementVehicleChangeDto? Vehicle { get; set; }
    public PolicyEndorsementDriverChangeDto? Driver { get; set; }
    public PolicyEndorsementCoverageChangeDto? Coverage { get; set; }
    public PolicyEndorsementPropertyChangeDto? Property { get; set; }
    public PolicyEndorsementCommercialChangeDto? Commercial { get; set; }
    public PolicyEndorsementFinancialChangeDto? Financial { get; set; }
    public PolicyEndorsementLegalChangeDto? Legal { get; set; }
}

public sealed class PolicyEndorsementInsuredChangeDto
{
    public Guid ChangeId { get; set; }
    public string? BeforeName { get; set; }
    public string? AfterName { get; set; }
    public string? BeforeDba { get; set; }
    public string? AfterDba { get; set; }
    public string? BeforeFein { get; set; }
    public string? AfterFein { get; set; }
    public string? BeforePhone { get; set; }
    public string? AfterPhone { get; set; }
    public string? BeforeEmail { get; set; }
    public string? AfterEmail { get; set; }
    public string? BeforeMailingAddress { get; set; }
    public string? AfterMailingAddress { get; set; }
    public string? BeforeGaragingAddress { get; set; }
    public string? AfterGaragingAddress { get; set; }
}

public sealed class PolicyEndorsementVehicleChangeDto
{
    public Guid ChangeId { get; set; }
    public Guid? BeforeVehicleId { get; set; }
    public Guid? AfterVehicleId { get; set; }
    public string? BeforeVin { get; set; }
    public string? AfterVin { get; set; }
    public int? BeforeYear { get; set; }
    public int? AfterYear { get; set; }
    public string? BeforeMake { get; set; }
    public string? AfterMake { get; set; }
    public string? BeforeModel { get; set; }
    public string? AfterModel { get; set; }
    public string? BeforeUsageCode { get; set; }
    public string? AfterUsageCode { get; set; }
    public string? BeforeGaragingAddress { get; set; }
    public string? AfterGaragingAddress { get; set; }
    public string? BeforeLienholder { get; set; }
    public string? AfterLienholder { get; set; }
}

public sealed class PolicyEndorsementDriverChangeDto
{
    public Guid ChangeId { get; set; }
    public Guid? BeforeDriverId { get; set; }
    public Guid? AfterDriverId { get; set; }
    public string? BeforeName { get; set; }
    public string? AfterName { get; set; }
    public string? BeforeLicenseNumber { get; set; }
    public string? AfterLicenseNumber { get; set; }
    public string? BeforeLicenseState { get; set; }
    public string? AfterLicenseState { get; set; }
    public DateOnly? BeforeBirthDate { get; set; }
    public DateOnly? AfterBirthDate { get; set; }
    public bool? BeforeExcluded { get; set; }
    public bool? AfterExcluded { get; set; }
}

public sealed class PolicyEndorsementCoverageChangeDto
{
    public Guid ChangeId { get; set; }
    public string? CoverageCode { get; set; }
    public string? BeforeCoverageName { get; set; }
    public string? AfterCoverageName { get; set; }
    public decimal? BeforeLimitAmount { get; set; }
    public decimal? AfterLimitAmount { get; set; }
    public string? BeforeLimitDescription { get; set; }
    public string? AfterLimitDescription { get; set; }
    public decimal? BeforeDeductibleAmount { get; set; }
    public decimal? AfterDeductibleAmount { get; set; }
    public decimal? BeforePremiumAmount { get; set; }
    public decimal? AfterPremiumAmount { get; set; }
}

public sealed class PolicyEndorsementPropertyChangeDto
{
    public Guid ChangeId { get; set; }
    public Guid? BeforePropertyId { get; set; }
    public Guid? AfterPropertyId { get; set; }
    public string? BeforeLocationAddress { get; set; }
    public string? AfterLocationAddress { get; set; }
    public string? BeforeBuildingNumber { get; set; }
    public string? AfterBuildingNumber { get; set; }
    public string? BeforeOccupancyCode { get; set; }
    public string? AfterOccupancyCode { get; set; }
    public string? BeforeConstructionCode { get; set; }
    public string? AfterConstructionCode { get; set; }
    public int? BeforeSquareFeet { get; set; }
    public int? AfterSquareFeet { get; set; }
    public decimal? BeforeBuildingValue { get; set; }
    public decimal? AfterBuildingValue { get; set; }
}

public sealed class PolicyEndorsementCommercialChangeDto
{
    public Guid ChangeId { get; set; }
    public string? ClassificationCode { get; set; }
    public decimal? BeforePayrollAmount { get; set; }
    public decimal? AfterPayrollAmount { get; set; }
    public decimal? BeforeRevenueAmount { get; set; }
    public decimal? AfterRevenueAmount { get; set; }
    public int? BeforeEmployeeCount { get; set; }
    public int? AfterEmployeeCount { get; set; }
    public decimal? BeforeEquipmentValue { get; set; }
    public decimal? AfterEquipmentValue { get; set; }
    public decimal? BeforeBlanketLimit { get; set; }
    public decimal? AfterBlanketLimit { get; set; }
    public int? BeforeLocationCount { get; set; }
    public int? AfterLocationCount { get; set; }
}

public sealed class PolicyEndorsementFinancialChangeDto
{
    public Guid ChangeId { get; set; }
    public string? BeforeBillingPlanCode { get; set; }
    public string? AfterBillingPlanCode { get; set; }
    public string? BeforeFinancingProvider { get; set; }
    public string? AfterFinancingProvider { get; set; }
    public int? BeforeInstallmentCount { get; set; }
    public int? AfterInstallmentCount { get; set; }
    public decimal? BeforeCommissionRate { get; set; }
    public decimal? AfterCommissionRate { get; set; }
    public decimal? BeforeCommissionAmount { get; set; }
    public decimal? AfterCommissionAmount { get; set; }
    public decimal? BeforeFinancedAmount { get; set; }
    public decimal? AfterFinancedAmount { get; set; }
}

public sealed class PolicyEndorsementLegalChangeDto
{
    public Guid ChangeId { get; set; }
    public string PartyTypeCode { get; set; } = string.Empty;
    public string? BeforePartyName { get; set; }
    public string? AfterPartyName { get; set; }
    public string? BeforeRelationshipCode { get; set; }
    public string? AfterRelationshipCode { get; set; }
    public string? BeforeAddress { get; set; }
    public string? AfterAddress { get; set; }
    public string? BeforeReferenceNumber { get; set; }
    public string? AfterReferenceNumber { get; set; }
}

public sealed class PolicyEndorsementApprovalDto
{
    public Guid ApprovalId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EndorsementId { get; set; }
    public string ApprovalLevelCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime RequestedDateUtc { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateTime? DecidedDateUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public string? DecisionNotes { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class PolicyEndorsementEventDto
{
    public Guid EventId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EndorsementId { get; set; }
    public Guid PolicyId { get; set; }
    public string EventTypeCode { get; set; } = string.Empty;
    public string? FromStatusCode { get; set; }
    public string? ToStatusCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? DataJson { get; set; }
    public Guid CorrelationId { get; set; }
    public DateTime OccurredDateUtc { get; set; }
    public Guid? ActorUserId { get; set; }
}

public sealed class PolicyEndorsementTransitionDto
{
    public Guid StatusTransitionId { get; set; }
    public string FromStatusCode { get; set; } = string.Empty;
    public string ToStatusCode { get; set; } = string.Empty;
    public string? RequiredPermissionCode { get; set; }
    public bool RequiresApproval { get; set; }
    public bool RequiresCarrierSubmission { get; set; }
    public bool CreatesPolicyVersion { get; set; }
    public bool CreatesAccountingWork { get; set; }
    public bool CreatesDocumentWork { get; set; }
}

public sealed class PolicyEndorsementCarrierDispatchDto
{
    public Guid CarrierDispatchId { get; set; }
    public string ChannelCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? ExternalReferenceNumber { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? NextAttemptDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class PolicyEndorsementAccountingWorkDto
{
    public Guid AccountingWorkId { get; set; }
    public string WorkTypeCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal PremiumAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? ResultEntityName { get; set; }
    public Guid? ResultEntityId { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class PolicyEndorsementDocumentWorkDto
{
    public Guid DocumentWorkId { get; set; }
    public string DocumentTypeCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public Guid? DocumentId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
}

public sealed class PolicyEndorsementCarrierDispatchWorkItem
{
    public Guid CarrierDispatchId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EndorsementId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid? CarrierConfigurationId { get; set; }
    public string ChannelCode { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestPayload { get; set; } = "{}";
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }
    public string? EndpointUri { get; set; }
    public string? HttpMethod { get; set; }
    public string? AuthenticationTypeCode { get; set; }
    public string? SecretReference { get; set; }
    public string? SenderAddress { get; set; }
    public string? RecipientAddress { get; set; }
    public string? PortalInstructions { get; set; }
    public string? PayloadTemplate { get; set; }
    public string? HeaderTemplate { get; set; }
    public int TimeoutSeconds { get; set; }
}
