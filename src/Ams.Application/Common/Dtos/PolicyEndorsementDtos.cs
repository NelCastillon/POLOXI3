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
    public string Description { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public DateTime RequestedDateUtc { get; set; }
    public decimal PremiumDelta { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public string AssignedToName { get; set; } = string.Empty;
    public string? UnderwriterName { get; set; }
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
}

public sealed class PolicyEndorsementDetailDto
{
    public PolicyEndorsementDto Endorsement { get; set; } = new();
    public IReadOnlyList<PolicyEndorsementActivityDto> Activities { get; set; } = [];
    public IReadOnlyList<PolicyEndorsementDeltaDto> Deltas { get; set; } = [];
}
