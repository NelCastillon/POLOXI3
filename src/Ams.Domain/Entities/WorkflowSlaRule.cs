using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class WorkflowSlaRule : AuditableEntity
{
    public Guid WorkflowDefinitionId { get; private set; }
    public int? StepOrder { get; private set; }
    public int SlaHours { get; private set; }
    public Guid? EscalationUserId { get; private set; }
    public string? EscalationRoleCode { get; private set; }
    public string? EscalationMessage { get; private set; }
    public bool IsActive { get; private set; }

    private WorkflowSlaRule() { }

    public WorkflowSlaRule(Guid? tenantId, Guid workflowDefinitionId, int slaHours, Guid? createdByUserId)
        : base(tenantId ?? Guid.Empty, createdByUserId)
    {
        WorkflowDefinitionId = workflowDefinitionId;
        SlaHours = slaHours;
        IsActive = true;
    }
}
