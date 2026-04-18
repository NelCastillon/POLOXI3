using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class EngagementTask : AuditableEntity
{
    public Guid EngagementId { get; private set; }
    public Guid? MilestoneId { get; private set; }
    public string TaskTitle { get; private set; } = string.Empty;
    public Guid? AssignedToUserId { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public DateOnly? CompletedDate { get; private set; }
    public TaskItemStatus Status { get; private set; } = TaskItemStatus.Open;
    public string Priority { get; private set; } = "Medium";

    private EngagementTask() { }

    public EngagementTask(Guid tenantId, Guid engagementId, string taskTitle, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        EngagementId = engagementId;
        TaskTitle = taskTitle;
        Status = TaskItemStatus.Open;
    }
}
