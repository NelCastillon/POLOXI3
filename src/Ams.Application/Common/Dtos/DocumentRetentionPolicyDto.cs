namespace Ams.Application.Common.Dtos;

public sealed record DocumentRetentionPolicyDto
{
    public Guid RetentionPolicyId { get; init; }
    public Guid TenantId { get; init; }
    public string PolicyName { get; init; } = string.Empty;
    public string PolicyCode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ApplicableCategory { get; init; }
    public string? ApplicableDocType { get; init; }
    public string? ApplicableEntityType { get; init; }
    public int RetentionPeriodYears { get; init; }
    public string RetentionStartTrigger { get; init; } = string.Empty;
    public string ActionOnExpiry { get; init; } = string.Empty;
    public bool RequireApprovalToDelete { get; init; }
    public int? NotifyBeforeDays { get; init; }
    public string? NotifyRoleCode { get; init; }
    public string? RegulatoryBasis { get; init; }
    public string? ComplianceNotes { get; init; }
    public bool IsActive { get; init; }
    public DateOnly EffectiveDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public DateTime CreatedDateUtc { get; init; }
    public DateTime? ModifiedDateUtc { get; init; }
}
