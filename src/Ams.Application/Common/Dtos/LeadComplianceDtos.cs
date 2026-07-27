namespace Ams.Application.Common.Dtos;

public sealed class PhoneComplianceReferenceDto
{
    public Guid PhoneComplianceReferenceId { get; set; }
    public string ReferenceTypeCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public sealed class LeadPhoneComplianceDto
{
    public Guid PhoneComplianceProfileId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid? LeadContactId { get; set; }
    public string EntityTypeCode { get; set; } = string.Empty;
    public string EntityDisplayName { get; set; } = string.Empty;
    public string NormalizedPhoneNumber { get; set; } = string.Empty;
    public string DisplayPhoneNumber { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string OverallStatusCode { get; set; } = string.Empty;
    public bool IsCallAllowed { get; set; }
    public bool IsSmsAllowed { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime? LastEvaluatedDateUtc { get; set; }
    public DateTime? NextScreeningDueDateUtc { get; set; }
    public IReadOnlyList<PhoneSuppressionDto> Suppressions { get; set; } = [];
    public IReadOnlyList<PhoneConsentDto> Consents { get; set; } = [];
    public IReadOnlyList<PhoneScreeningResultDto> ScreeningResults { get; set; } = [];
}

public sealed class PhoneSuppressionDto
{
    public Guid PhoneSuppressionId { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string ChannelCode { get; set; } = string.Empty;
    public string? PurposeCode { get; set; }
    public string? JurisdictionCode { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime EffectiveDateUtc { get; set; }
    public DateTime? ExpirationDateUtc { get; set; }
    public DateTime? RequestedDateUtc { get; set; }
    public string? Notes { get; set; }
    public string? EvidenceReference { get; set; }
    public DateTime? RevokedDateUtc { get; set; }
    public string? RevocationReason { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class PhoneConsentDto
{
    public Guid PhoneConsentId { get; set; }
    public string ConsentTypeCode { get; set; } = string.Empty;
    public string ChannelCode { get; set; } = string.Empty;
    public string PurposeCode { get; set; } = string.Empty;
    public string LegalBasisCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CapturedDateUtc { get; set; }
    public DateTime EffectiveDateUtc { get; set; }
    public DateTime? ExpirationDateUtc { get; set; }
    public string EvidenceTypeCode { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string? ConsentText { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public DateTime? RevokedDateUtc { get; set; }
    public string? RevocationReason { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class PhoneScreeningResultDto
{
    public Guid PhoneScreeningResultId { get; set; }
    public Guid? PhoneScreeningBatchId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string RegistryCode { get; set; } = string.Empty;
    public string? JurisdictionCode { get; set; }
    public string ResultCode { get; set; } = string.Empty;
    public DateTime ScreenedDateUtc { get; set; }
    public DateTime? ValidThroughDateUtc { get; set; }
    public string? ProviderReference { get; set; }
    public string? ErrorDetails { get; set; }
}

public sealed class PhoneContactEligibilityDto
{
    public Guid PhoneComplianceProfileId { get; set; }
    public string NormalizedPhoneNumber { get; set; } = string.Empty;
    public string ChannelCode { get; set; } = string.Empty;
    public string PurposeCode { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
    public string DecisionCode { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string DecisionSummary { get; set; } = string.Empty;
    public DateTime EvaluatedDateUtc { get; set; }
}

public sealed class DuePhoneScreeningDto
{
    public Guid PhoneComplianceProfileId { get; set; }
    public Guid TenantId { get; set; }
    public string NormalizedPhoneNumber { get; set; } = string.Empty;
    public DateTime? NextScreeningDueDateUtc { get; set; }
}

public sealed class PhoneComplianceWorkspaceDto
{
    public int TotalPhones { get; set; }
    public int SuppressedPhones { get; set; }
    public int PendingScreeningPhones { get; set; }
    public int AllowedPhones { get; set; }
    public int DueScreenings { get; set; }
    public int FailedScreenings { get; set; }
    public IReadOnlyList<PhoneComplianceRegistryRowDto> Phones { get; set; } = [];
}

public sealed class PhoneComplianceRegistryRowDto
{
    public Guid PhoneComplianceProfileId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public string LeadName { get; set; } = string.Empty;
    public string LeadNumber { get; set; } = string.Empty;
    public string DisplayPhoneNumber { get; set; } = string.Empty;
    public string NormalizedPhoneNumber { get; set; } = string.Empty;
    public string OverallStatusCode { get; set; } = string.Empty;
    public bool IsCallAllowed { get; set; }
    public bool IsSmsAllowed { get; set; }
    public int ActiveSuppressionCount { get; set; }
    public int ActiveConsentCount { get; set; }
    public string? LatestScreeningResultCode { get; set; }
    public string? LatestScreeningProviderCode { get; set; }
    public DateTime? LastScreenedDateUtc { get; set; }
    public DateTime? NextScreeningDueDateUtc { get; set; }
}
