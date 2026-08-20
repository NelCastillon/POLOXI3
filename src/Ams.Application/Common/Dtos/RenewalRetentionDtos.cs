namespace Ams.Application.Common.Dtos;

public sealed class RenewalRetentionCaseDto
{
    public Guid RetentionCaseId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? SourcePolicyTermId { get; set; }
    public Guid? RenewalOpportunityId { get; set; }
    public Guid? RenewalSubmissionId { get; set; }
    public Guid? RenewalPolicyBindTransactionId { get; set; }
    public Guid? ResultPolicyId { get; set; }
    public Guid? ResultPolicyTermId { get; set; }
    public string? InitiationSourceCode { get; set; }
    public DateTime? InitiatedDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
    public string Csr { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public decimal CurrentPremium { get; set; }
    public decimal? ProposedPremium { get; set; }
    public int RetentionProbability { get; set; }
    public int RiskScore { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string OutreachStatus { get; set; } = string.Empty;
    public string Sentiment { get; set; } = string.Empty;
    public string? RiskDrivers { get; set; }
    public string? NextBestAction { get; set; }
    public DateTime? NextActionDueDate { get; set; }
    public DateTime? LastTouchDateUtc { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public bool IsEscalated { get; set; }
    public bool IsAtRisk { get; set; }
    public bool IsSaved { get; set; }
    public int DaysToExpiration => (ExpirationDate.Date - DateTime.UtcNow.Date).Days;
    public decimal PremiumDelta => (ProposedPremium ?? CurrentPremium) - CurrentPremium;
}

public sealed class RenewalRetentionActivityDto
{
    public Guid RetentionActivityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RetentionCaseId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime ActivityDateUtc { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
}

public sealed class RenewalRetentionOfferDto
{
    public Guid RetentionOfferId { get; set; }
    public Guid TenantId { get; set; }
    public Guid RetentionCaseId { get; set; }
    public string OfferName { get; set; } = string.Empty;
    public string OfferType { get; set; } = string.Empty;
    public decimal PremiumImpact { get; set; }
    public int RetentionLift { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PresentedDateUtc { get; set; }
    public DateTime? AcceptedDateUtc { get; set; }
    public string? Notes { get; set; }
}

public sealed class RenewalRetentionCenterDto
{
    public IReadOnlyList<RenewalRetentionCaseDto> Cases { get; set; } = [];
    public IReadOnlyList<RenewalRetentionActivityDto> Activities { get; set; } = [];
    public IReadOnlyList<RenewalRetentionOfferDto> Offers { get; set; } = [];
    public IReadOnlyList<RenewalWorkflowOptionDto> Options { get; set; } = [];
}

public sealed class RenewalWorkflowOptionDto
{
    public Guid WorkflowOptionId { get; set; }
    public string OptionGroupCode { get; set; } = string.Empty;
    public string OptionCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}

public sealed class RenewalInitiationResultDto
{
    public int EligiblePolicyTerms { get; set; }
    public int CreatedCases { get; set; }
}

public sealed class RenewalRetentionDetailDto
{
    public RenewalRetentionCaseDto Case { get; set; } = new();
    public IReadOnlyList<RenewalRetentionActivityDto> Activities { get; set; } = [];
    public IReadOnlyList<RenewalRetentionOfferDto> Offers { get; set; } = [];
}
