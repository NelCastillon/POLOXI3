namespace Ams.Domain.Entities;

public sealed class DocumentAuditTrail
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid DocumentId { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string EventCategory { get; init; } = "Document";
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
    public int RetentionYears { get; init; } = 7;
    public bool IsArchived { get; private set; }
    public DateTime CreatedDateUtc { get; init; }

    private DocumentAuditTrail() { }

    public DocumentAuditTrail(
        Guid tenantId,
        Guid documentId,
        Guid? workflowInstanceId,
        string eventType,
        string eventCategory,
        string? eventDescription,
        Guid? performedByUserId,
        string? performedByName,
        string? performedByRoleCode,
        string? oldValue,
        string? newValue,
        string? changesSummary,
        string? ipAddress,
        string? userAgent,
        string? sessionId,
        int retentionYears)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        DocumentId = documentId;
        WorkflowInstanceId = workflowInstanceId;
        EventType = eventType;
        EventCategory = eventCategory;
        EventDescription = eventDescription;
        PerformedByUserId = performedByUserId;
        PerformedByName = performedByName;
        PerformedByRoleCode = performedByRoleCode;
        EventDateUtc = DateTime.UtcNow;
        OldValue = oldValue;
        NewValue = newValue;
        ChangesSummary = changesSummary;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        SessionId = sessionId;
        RetentionYears = retentionYears;
        CreatedDateUtc = DateTime.UtcNow;
        IsArchived = false;
    }

    public void Archive()
    {
        IsArchived = true;
    }
}
