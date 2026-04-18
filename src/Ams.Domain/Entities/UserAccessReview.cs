using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class UserAccessReview : AuditableEntity
{
    public string ReviewCycleCode { get; private set; } = "Annual";
    public Guid ReviewerUserId { get; private set; }
    public Guid SubjectUserId { get; private set; }
    public Guid RoleId { get; private set; }
    public AccessReviewDecision Decision { get; private set; } = AccessReviewDecision.Pending;
    public string? DecisionNotes { get; private set; }
    public DateTime? ReviewedDateUtc { get; private set; }
    public DateTime DueByDateUtc { get; private set; }
    public string StatusCode { get; private set; } = "Pending";

    private UserAccessReview() { }

    public UserAccessReview(Guid tenantId, string reviewCycleCode, Guid reviewerUserId, Guid subjectUserId, Guid roleId, DateTime dueByDateUtc, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        ReviewCycleCode = reviewCycleCode;
        ReviewerUserId = reviewerUserId;
        SubjectUserId = subjectUserId;
        RoleId = roleId;
        DueByDateUtc = dueByDateUtc;
    }
}
