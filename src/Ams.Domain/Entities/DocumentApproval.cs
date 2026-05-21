using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class DocumentApproval : AuditableEntity
{
    public Guid WorkflowInstanceId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid? StepTemplateId { get; private set; }
    public string ApprovalName { get; private set; } = string.Empty;
    public string ApprovalType { get; private set; } = "Standard";
    public int StepOrder { get; private set; }
    public Guid AssignedToUserId { get; private set; }
    public string? AssignedToName { get; private set; }
    public string? AssignedToRoleCode { get; private set; }
    public DateTime AssignedDateUtc { get; private set; }
    public string ApprovalStatus { get; private set; } = "Pending";
    public DateTime? ResponseDateUtc { get; private set; }
    public Guid? ResponseByUserId { get; private set; }
    public string? ResponseByName { get; private set; }
    public string? Comments { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public DateTime? EscalatedDateUtc { get; private set; }
    public Guid? EscalatedToUserId { get; private set; }

    private DocumentApproval() { }

    public DocumentApproval(
        Guid tenantId,
        Guid workflowInstanceId,
        Guid documentId,
        Guid? stepTemplateId,
        string approvalName,
        string approvalType,
        int stepOrder,
        Guid assignedToUserId,
        string? assignedToName,
        string? assignedToRoleCode,
        DateTime? dueDateUtc,
        Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        WorkflowInstanceId = workflowInstanceId;
        DocumentId = documentId;
        StepTemplateId = stepTemplateId;
        ApprovalName = approvalName;
        ApprovalType = approvalType;
        StepOrder = stepOrder;
        AssignedToUserId = assignedToUserId;
        AssignedToName = assignedToName;
        AssignedToRoleCode = assignedToRoleCode;
        AssignedDateUtc = DateTime.UtcNow;
        DueDateUtc = dueDateUtc;
        ApprovalStatus = "Pending";
    }

    public void Approve(Guid responseByUserId, string? responseByName, string? comments, Guid? modifiedByUserId)
    {
        ApprovalStatus = "Approved";
        ResponseDateUtc = DateTime.UtcNow;
        ResponseByUserId = responseByUserId;
        ResponseByName = responseByName;
        Comments = comments;
        MarkModified(modifiedByUserId);
    }

    public void Reject(Guid responseByUserId, string? responseByName, string? comments, Guid? modifiedByUserId)
    {
        ApprovalStatus = "Rejected";
        ResponseDateUtc = DateTime.UtcNow;
        ResponseByUserId = responseByUserId;
        ResponseByName = responseByName;
        Comments = comments;
        MarkModified(modifiedByUserId);
    }

    public void Defer(string? comments, Guid? modifiedByUserId)
    {
        ApprovalStatus = "Deferred";
        Comments = comments;
        MarkModified(modifiedByUserId);
    }

    public void Escalate(Guid escalatedToUserId, Guid? modifiedByUserId)
    {
        ApprovalStatus = "Escalated";
        EscalatedDateUtc = DateTime.UtcNow;
        EscalatedToUserId = escalatedToUserId;
        MarkModified(modifiedByUserId);
    }

    public void Reassign(Guid newAssignedToUserId, string? newAssignedToName, Guid? modifiedByUserId)
    {
        AssignedToUserId = newAssignedToUserId;
        AssignedToName = newAssignedToName;
        MarkModified(modifiedByUserId);
    }
}
