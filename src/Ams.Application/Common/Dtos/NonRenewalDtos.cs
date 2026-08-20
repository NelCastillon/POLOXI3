namespace Ams.Application.Common.Dtos;

public sealed class NonRenewalDto
{
    public Guid NonRenewalId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? AccountId { get; set; }
    public string NonRenewalNumber { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? CarrierName { get; set; }
    public string? LineOfBusiness { get; set; }
    public string? StateCode { get; set; }
    public DateTime? PolicyExpirationDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public string InitiatedByCode { get; set; } = string.Empty;
    public DateTime? CarrierNoticeDate { get; set; }
    public string? CarrierNoticeMethodCode { get; set; }
    public string? CarrierNoticeReference { get; set; }
    public string? CarrierNoticeSummary { get; set; }
    public int? RequiredNoticeDays { get; set; }
    public DateTime? NoticeDeadlineDate { get; set; }
    public bool? IsNoticeCompliant { get; set; }
    public DateTime? InsuredNotifiedDate { get; set; }
    public string? InsuredNotificationMethodCode { get; set; }
    public string? InsuredNotificationProofReference { get; set; }
    public string? InsuredNotificationSentByName { get; set; }
    public bool RemarketRecommended { get; set; }
    public Guid? RemarketSubmissionId { get; set; }
    public string? ResolutionSummary { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public string? Notes { get; set; }
    public bool IsUrgent { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public int? DaysToExpiration => PolicyExpirationDate is null ? null : (PolicyExpirationDate.Value.Date - DateTime.UtcNow.Date).Days;
    public bool IsDeadlinePassed => NoticeDeadlineDate is not null && InsuredNotifiedDate is null && NoticeDeadlineDate.Value.Date < DateTime.UtcNow.Date;
}

public sealed class NonRenewalActivityDto
{
    public Guid ActivityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid NonRenewalId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime ActivityDateUtc { get; set; }
}

public sealed class NonRenewalStatusDto
{
    public Guid NonRenewalStatusId { get; set; }
    public Guid TenantId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ColorHex { get; set; }
    public bool IsTerminal { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}

public sealed class NonRenewalReasonDto
{
    public Guid NonRenewalReasonId { get; set; }
    public Guid TenantId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string ReasonName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public bool IsRemarketRecommended { get; set; }
    public int SortOrder { get; set; }
}

public sealed class NonRenewalStateRequirementDto
{
    public Guid NonRenewalStateRequirementId { get; set; }
    public Guid TenantId { get; set; }
    public string StateCode { get; set; } = string.Empty;
    public string StateName { get; set; } = string.Empty;
    public string LineCategoryCode { get; set; } = string.Empty;
    public int MinimumNoticeDays { get; set; }
    public int InsuredNoticeDays { get; set; }
    public string? Notes { get; set; }
}

public sealed class NonRenewalEligiblePolicyDto
{
    public Guid PolicyId { get; set; }
    public Guid? AccountId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? CarrierName { get; set; }
    public string? LineOfBusiness { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
}

public sealed class NonRenewalCenterDto
{
    public IReadOnlyList<NonRenewalDto> NonRenewals { get; set; } = [];
    public IReadOnlyList<NonRenewalStatusDto> Statuses { get; set; } = [];
    public IReadOnlyList<NonRenewalReasonDto> Reasons { get; set; } = [];
    public IReadOnlyList<NonRenewalStateRequirementDto> StateRequirements { get; set; } = [];
    public IReadOnlyList<NonRenewalEligiblePolicyDto> EligiblePolicies { get; set; } = [];
}

public sealed class NonRenewalDetailDto
{
    public NonRenewalDto NonRenewal { get; set; } = new();
    public IReadOnlyList<NonRenewalActivityDto> Activities { get; set; } = [];
}
