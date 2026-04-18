using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class WorkflowReworkRequest : AuditableEntity
{
    public Guid WorkflowInstanceId { get; private set; }
    public Guid? ApprovalStepId { get; private set; }
    public Guid? RequestedByUserId { get; private set; }
    public string RejectionReason { get; private set; } = string.Empty;
    public string? ReworkInstructions { get; private set; }
    public int? ReturnToStepOrder { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;
    public Guid? ResubmittedByUserId { get; private set; }
    public DateTime? ResubmittedDateUtc { get; private set; }
    public DateTime? ResolvedDateUtc { get; private set; }

    private WorkflowReworkRequest() { }

    public WorkflowReworkRequest(Guid tenantId, Guid workflowInstanceId, string rejectionReason, Guid? requestedByUserId)
        : base(tenantId, requestedByUserId)
    {
        WorkflowInstanceId = workflowInstanceId;
        RejectionReason = rejectionReason;
        RequestedByUserId = requestedByUserId;
        StatusCode = "Pending";
    }
}
