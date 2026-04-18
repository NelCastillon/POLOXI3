using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class ServiceIssue : AuditableEntity
{
    public Guid? EngagementId { get; private set; }
    public Guid? AccountId { get; private set; }
    public string IssueNumber { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public IssueSeverity Severity { get; private set; } = IssueSeverity.Medium;
    public Guid? AssignedToUserId { get; private set; }
    public IssueStatus Status { get; private set; } = IssueStatus.Open;
    public DateOnly? ResolvedDate { get; private set; }

    private ServiceIssue() { }

    public ServiceIssue(Guid tenantId, string issueNumber, string title, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        IssueNumber = issueNumber;
        Title = title;
        Severity = IssueSeverity.Medium;
        Status = IssueStatus.Open;
    }
}
