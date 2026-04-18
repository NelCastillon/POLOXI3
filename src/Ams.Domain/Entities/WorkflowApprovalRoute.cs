using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class WorkflowApprovalRoute : AuditableEntity
{
    public Guid WorkflowDefinitionId { get; private set; }
    public int StepOrder { get; private set; }
    public string StepName { get; private set; } = string.Empty;
    public Guid? ApproverUserId { get; private set; }
    public string? ApproverRoleCode { get; private set; }
    public Guid? ApproverGroupId { get; private set; }
    public decimal? ThresholdMinAmount { get; private set; }
    public decimal? ThresholdMaxAmount { get; private set; }
    public bool RequireAllApprovers { get; private set; }
    public bool IsActive { get; private set; }

    private WorkflowApprovalRoute() { }

    public WorkflowApprovalRoute(Guid? tenantId, Guid workflowDefinitionId, int stepOrder, string stepName, Guid? createdByUserId)
        : base(tenantId ?? Guid.Empty, createdByUserId)
    {
        WorkflowDefinitionId = workflowDefinitionId;
        StepOrder = stepOrder;
        StepName = stepName;
        IsActive = true;
    }
}
