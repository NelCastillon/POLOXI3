using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class WorkflowApprovalHistory : AuditableEntity
{
    public Guid WorkflowInstanceId { get; private set; }
    public Guid? ApprovalStepId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActionCode { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public string? PreviousStatusCode { get; private set; }
    public string? NewStatusCode { get; private set; }
    public bool IsDelegated { get; private set; }
    public Guid? DelegatedByUserId { get; private set; }
    public DateTime ActionDateUtc { get; private set; }

    private WorkflowApprovalHistory() { }

    public WorkflowApprovalHistory(Guid tenantId, Guid workflowInstanceId, string actionCode, Guid? actorUserId)
        : base(tenantId, actorUserId)
    {
        WorkflowInstanceId = workflowInstanceId;
        ActionCode = actionCode;
        ActorUserId = actorUserId;
        ActionDateUtc = DateTime.UtcNow;
    }

    public WorkflowApprovalHistory(Guid tenantId, Guid workflowInstanceId, string actionCode, Guid? actorUserId,
        Guid? approvalStepId, string? notes, string? previousStatusCode, string? newStatusCode,
        bool isDelegated, Guid? delegatedByUserId)
        : base(tenantId, actorUserId)
    {
        WorkflowInstanceId = workflowInstanceId;
        ActionCode = actionCode;
        ActorUserId = actorUserId;
        ActionDateUtc = DateTime.UtcNow;
        ApprovalStepId = approvalStepId;
        Notes = notes;
        PreviousStatusCode = previousStatusCode;
        NewStatusCode = newStatusCode;
        IsDelegated = isDelegated;
        DelegatedByUserId = delegatedByUserId;
    }
}
