namespace Ams.Application.Common.Dtos;

public sealed class PolicyCancellationDto
{
    public Guid CancellationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? AccountId { get; set; }
    public string CancellationNumber { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string CancellationReason { get; set; } = string.Empty;
    public string CancellationType { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public DateTime RequestDateUtc { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? CancellationDate { get; set; }
    public DateTime? ReinstatementDate { get; set; }
    public decimal ReturnPremium { get; set; }
    public decimal PremiumDue { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public string AssignedToName { get; set; } = string.Empty;
    public string? ApprovedByName { get; set; }
    public string? ReinstatedByName { get; set; }
    public string? Notes { get; set; }
    public string? WorkflowStage { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ApprovedDateUtc { get; set; }
    public bool IsUrgent { get; set; }
    public bool IsArchived { get; set; }
    public int DaysOpen => Math.Max(0, (DateTime.UtcNow.Date - RequestDateUtc.Date).Days);
    public int LapseDays => CancellationDate is not null && ReinstatementDate is not null
        ? Math.Max(0, (ReinstatementDate.Value.Date - CancellationDate.Value.Date).Days)
        : 0;
}

public sealed class PolicyCancellationActivityDto
{
    public Guid ActivityId { get; set; }
    public Guid CancellationId { get; set; }
    public Guid TenantId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime ActivityDateUtc { get; set; }
}

public sealed class PolicyCancellationCenterDto
{
    public IReadOnlyList<PolicyCancellationDto> Cancellations { get; set; } = [];
    public IReadOnlyList<PolicyCancellationActivityDto> Activities { get; set; } = [];
}

public sealed class PolicyCancellationDetailDto
{
    public PolicyCancellationDto Cancellation { get; set; } = new();
    public IReadOnlyList<PolicyCancellationActivityDto> Activities { get; set; } = [];
}
