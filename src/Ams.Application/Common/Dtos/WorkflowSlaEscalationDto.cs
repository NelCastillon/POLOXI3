namespace Ams.Application.Common.Dtos;

public sealed class WorkflowSlaEscalationDto
{
    public Guid EscalationId { get; init; }
    public Guid TenantId { get; init; }
    public Guid WorkflowInstanceId { get; init; }
    public Guid? ApprovalStepId { get; init; }
    public Guid? SlaRuleId { get; init; }
    public Guid? EscalatedToUserId { get; init; }
    public DateTime EscalationDateUtc { get; init; }
    public int BreachHours { get; init; }
    public DateTime? NotificationSentDateUtc { get; init; }
    public string StatusCode { get; init; } = string.Empty;
    public DateTime? ResolvedDateUtc { get; init; }
    public DateTime CreatedDateUtc { get; init; }
}
