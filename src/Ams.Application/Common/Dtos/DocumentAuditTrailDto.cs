namespace Ams.Application.Common.Dtos;

public sealed record DocumentAuditTrailDto
{
    public Guid AuditId { get; init; }
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public Guid DocumentId { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string EventCategory { get; init; } = string.Empty;
    public string? EventDescription { get; init; }
    public Guid? PerformedByUserId { get; init; }
    public string? PerformedByName { get; init; }
    public string? PerformedByRoleCode { get; init; }
    public DateTime EventDateUtc { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string? ChangesSummary { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? SessionId { get; init; }
    public int RetentionYears { get; init; }
    public bool IsArchived { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
