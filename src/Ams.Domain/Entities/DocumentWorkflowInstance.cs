using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class DocumentWorkflowInstance : AuditableEntity
{
    public Guid DocumentId { get; private set; }
    public Guid WorkflowTemplateId { get; private set; }
    public string InstanceName { get; private set; } = string.Empty;
    public string WorkflowStatus { get; private set; } = "Pending";
    public int? CurrentStepOrder { get; private set; }
    public DateTime StartedDateUtc { get; private set; }
    public DateTime? CompletedDateUtc { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public Guid InitiatedByUserId { get; private set; }
    public string? InitiatedByName { get; private set; }
    public string? Comments { get; private set; }
    public string Priority { get; private set; } = "Normal";
    public string? FinalOutcome { get; private set; }
    public string? FinalComments { get; private set; }
    public Guid? CompletedByUserId { get; private set; }
    public string? CompletedByName { get; private set; }

    private DocumentWorkflowInstance() { }

    public DocumentWorkflowInstance(
        Guid tenantId,
        Guid documentId,
        Guid workflowTemplateId,
        string instanceName,
        Guid initiatedByUserId,
        string? initiatedByName,
        string? comments,
        string priority,
        DateTime? dueDateUtc,
        Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        DocumentId = documentId;
        WorkflowTemplateId = workflowTemplateId;
        InstanceName = instanceName;
        InitiatedByUserId = initiatedByUserId;
        InitiatedByName = initiatedByName;
        Comments = comments;
        Priority = priority;
        DueDateUtc = dueDateUtc;
        StartedDateUtc = DateTime.UtcNow;
        WorkflowStatus = "Pending";
    }

    public void Start(int firstStepOrder, Guid? modifiedByUserId)
    {
        WorkflowStatus = "InProgress";
        CurrentStepOrder = firstStepOrder;
        MarkModified(modifiedByUserId);
    }

    public void AdvanceToNextStep(int nextStepOrder, Guid? modifiedByUserId)
    {
        CurrentStepOrder = nextStepOrder;
        MarkModified(modifiedByUserId);
    }

    public void Complete(string finalOutcome, string? finalComments, Guid completedByUserId, string? completedByName, Guid? modifiedByUserId)
    {
        WorkflowStatus = "Completed";
        FinalOutcome = finalOutcome;
        FinalComments = finalComments;
        CompletedByUserId = completedByUserId;
        CompletedByName = completedByName;
        CompletedDateUtc = DateTime.UtcNow;
        MarkModified(modifiedByUserId);
    }

    public void Reject(string? finalComments, Guid completedByUserId, string? completedByName, Guid? modifiedByUserId)
    {
        WorkflowStatus = "Rejected";
        FinalOutcome = "Rejected";
        FinalComments = finalComments;
        CompletedByUserId = completedByUserId;
        CompletedByName = completedByName;
        CompletedDateUtc = DateTime.UtcNow;
        MarkModified(modifiedByUserId);
    }

    public void Cancel(string? reason, Guid? modifiedByUserId)
    {
        WorkflowStatus = "Cancelled";
        FinalOutcome = "Cancelled";
        FinalComments = reason;
        CompletedDateUtc = DateTime.UtcNow;
        MarkModified(modifiedByUserId);
    }

    public void Escalate(Guid? modifiedByUserId)
    {
        WorkflowStatus = "Escalated";
        MarkModified(modifiedByUserId);
    }
}
