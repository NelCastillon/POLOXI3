using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class EngagementMilestone : AuditableEntity
{
    public Guid EngagementId { get; private set; }
    public string MilestoneName { get; private set; } = string.Empty;
    public DateOnly? DueDate { get; private set; }
    public DateOnly? CompletedDate { get; private set; }
    public MilestoneStatus Status { get; private set; } = MilestoneStatus.Pending;

    private EngagementMilestone() { }

    public EngagementMilestone(Guid tenantId, Guid engagementId, string milestoneName, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        EngagementId = engagementId;
        MilestoneName = milestoneName;
        Status = MilestoneStatus.Pending;
    }
}
