namespace Ams.Application.Common.Dtos;

public sealed class PolicyCheckDto
{
    public Guid PolicyCheckId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? QuoteId { get; set; }
    public string CheckNumber { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? CarrierName { get; set; }
    public string? LineOfBusiness { get; set; }
    public DateTime? PolicyEffectiveDate { get; set; }
    public DateTime? PolicyExpirationDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public string CheckTypeCode { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ReceivedDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public string? CompletedByName { get; set; }
    public int ItemsTotal { get; set; }
    public int ItemsMatched { get; set; }
    public int ItemsDiscrepant { get; set; }
    public string? ResultSummary { get; set; }
    public string? Notes { get; set; }
    public bool IsUrgent { get; set; }
    public bool IsArchived { get; set; }
    public int DaysOpen => ReceivedDateUtc is null ? 0 : Math.Max(0, ((CompletedDateUtc ?? DateTime.UtcNow).Date - ReceivedDateUtc.Value.Date).Days);
    public bool IsOverdue => DueDate is not null && CompletedDateUtc is null && DueDate.Value.Date < DateTime.UtcNow.Date;
}

public sealed class PolicyCheckItemDto
{
    public Guid PolicyCheckItemId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyCheckId { get; set; }
    public Guid? PolicyCheckItemDefinitionId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public string MatchStatusCode { get; set; } = string.Empty;
    public string SeverityCode { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? Notes { get; set; }
    public string? CheckedByName { get; set; }
    public DateTime? CheckedDateUtc { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PolicyCheckDiscrepancyDto
{
    public Guid PolicyCheckDiscrepancyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyCheckId { get; set; }
    public Guid? PolicyCheckItemId { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string SeverityCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool CarrierNotified { get; set; }
    public DateTime? CarrierNotifiedDateUtc { get; set; }
    public string? CarrierReferenceNumber { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class PolicyCheckActivityDto
{
    public Guid ActivityId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PolicyCheckId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime ActivityDateUtc { get; set; }
}

public sealed class PolicyCheckStatusDto
{
    public Guid PolicyCheckStatusId { get; set; }
    public Guid TenantId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ColorHex { get; set; }
    public bool IsTerminal { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PolicyCheckItemDefinitionDto
{
    public Guid PolicyCheckItemDefinitionId { get; set; }
    public Guid TenantId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DefaultSeverityCode { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PolicyCheckDiscrepancyTypeDto
{
    public Guid PolicyCheckDiscrepancyTypeId { get; set; }
    public Guid TenantId { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DefaultSeverityCode { get; set; } = string.Empty;
    public bool RequiresCarrierNotification { get; set; }
    public int SortOrder { get; set; }
}

public sealed class PolicyCheckEligiblePolicyDto
{
    public Guid PolicyId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? QuoteId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? CarrierName { get; set; }
    public string? LineOfBusiness { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public DateTime? ExpirationDate { get; set; }
}

public sealed class PolicyCheckCenterDto
{
    public IReadOnlyList<PolicyCheckDto> Checks { get; set; } = [];
    public IReadOnlyList<PolicyCheckStatusDto> Statuses { get; set; } = [];
    public IReadOnlyList<PolicyCheckItemDefinitionDto> ItemDefinitions { get; set; } = [];
    public IReadOnlyList<PolicyCheckDiscrepancyTypeDto> DiscrepancyTypes { get; set; } = [];
    public IReadOnlyList<PolicyCheckEligiblePolicyDto> EligiblePolicies { get; set; } = [];
}

public sealed class PolicyCheckDetailDto
{
    public PolicyCheckDto Check { get; set; } = new();
    public IReadOnlyList<PolicyCheckItemDto> Items { get; set; } = [];
    public IReadOnlyList<PolicyCheckDiscrepancyDto> Discrepancies { get; set; } = [];
    public IReadOnlyList<PolicyCheckActivityDto> Activities { get; set; } = [];
}
