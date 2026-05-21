using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class DocumentReview : AuditableEntity
{
    public Guid? WorkflowInstanceId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string ReviewName { get; private set; } = string.Empty;
    public string ReviewType { get; private set; } = "Standard";
    public string? ReviewPurpose { get; private set; }
    public Guid AssignedToUserId { get; private set; }
    public string? AssignedToName { get; private set; }
    public DateTime AssignedDateUtc { get; private set; }
    public string ReviewStatus { get; private set; } = "Pending";
    public DateTime? CompletedDateUtc { get; private set; }
    public Guid? CompletedByUserId { get; private set; }
    public string? CompletedByName { get; private set; }
    public string? ReviewNotes { get; private set; }
    public int? Rating { get; private set; }
    public int IssuesFound { get; private set; }
    public bool RecommendChanges { get; private set; }
    public string? ChangesDescription { get; private set; }
    public DateTime? DueDateUtc { get; private set; }

    private DocumentReview() { }

    public DocumentReview(
        Guid tenantId,
        Guid documentId,
        Guid? workflowInstanceId,
        string reviewName,
        string reviewType,
        string? reviewPurpose,
        Guid assignedToUserId,
        string? assignedToName,
        DateTime? dueDateUtc,
        Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        DocumentId = documentId;
        WorkflowInstanceId = workflowInstanceId;
        ReviewName = reviewName;
        ReviewType = reviewType;
        ReviewPurpose = reviewPurpose;
        AssignedToUserId = assignedToUserId;
        AssignedToName = assignedToName;
        AssignedDateUtc = DateTime.UtcNow;
        DueDateUtc = dueDateUtc;
        ReviewStatus = "Pending";
    }

    public void StartReview(Guid? modifiedByUserId)
    {
        ReviewStatus = "InReview";
        MarkModified(modifiedByUserId);
    }

    public void Complete(
        Guid completedByUserId,
        string? completedByName,
        string? reviewNotes,
        int? rating,
        int issuesFound,
        bool recommendChanges,
        string? changesDescription,
        Guid? modifiedByUserId)
    {
        ReviewStatus = "Completed";
        CompletedDateUtc = DateTime.UtcNow;
        CompletedByUserId = completedByUserId;
        CompletedByName = completedByName;
        ReviewNotes = reviewNotes;
        Rating = rating;
        IssuesFound = issuesFound;
        RecommendChanges = recommendChanges;
        ChangesDescription = changesDescription;
        MarkModified(modifiedByUserId);
    }

    public void Return(string? reviewNotes, Guid? modifiedByUserId)
    {
        ReviewStatus = "Returned";
        ReviewNotes = reviewNotes;
        MarkModified(modifiedByUserId);
    }

    public void Cancel(string? reason, Guid? modifiedByUserId)
    {
        ReviewStatus = "Cancelled";
        ReviewNotes = reason;
        MarkModified(modifiedByUserId);
    }

    public void Reassign(Guid newAssignedToUserId, string? newAssignedToName, Guid? modifiedByUserId)
    {
        AssignedToUserId = newAssignedToUserId;
        AssignedToName = newAssignedToName;
        MarkModified(modifiedByUserId);
    }
}
