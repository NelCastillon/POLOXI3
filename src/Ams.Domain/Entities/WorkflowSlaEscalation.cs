using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class WorkflowSlaEscalation : AuditableEntity
{
    public Guid WorkflowInstanceId { get; private set; }
    public Guid? ApprovalStepId { get; private set; }
    public Guid? SlaRuleId { get; private set; }
    public Guid? EscalatedToUserId { get; private set; }
    public DateTime EscalationDateUtc { get; private set; }
    public int BreachHours { get; private set; }
    public DateTime? NotificationSentDateUtc { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;
    public DateTime? ResolvedDateUtc { get; private set; }

    private WorkflowSlaEscalation() { }

    public WorkflowSlaEscalation(Guid tenantId, Guid workflowInstanceId, int breachHours)
        : base(tenantId, null)
    {
        WorkflowInstanceId = workflowInstanceId;
        EscalationDateUtc = DateTime.UtcNow;
        BreachHours = breachHours;
        StatusCode = "Open";
    }
}
