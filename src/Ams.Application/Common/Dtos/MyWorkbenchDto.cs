namespace Ams.Application.Common.Dtos;

public sealed class MyWorkbenchDto
{
    public MyWorkbenchKpiDto Kpis { get; set; } = new();
    public IReadOnlyList<MyWorkbenchTaskDto> Tasks { get; set; } = [];
    public IReadOnlyList<MyWorkbenchScheduleEventDto> Schedule { get; set; } = [];
    public IReadOnlyList<MyWorkbenchActivityDto> Activities { get; set; } = [];
    public IReadOnlyList<MyWorkbenchNotificationDto> Notifications { get; set; } = [];
    public IReadOnlyList<MyWorkbenchQuickLinkDto> QuickLinks { get; set; } = [];
}

public sealed class MyWorkbenchKpiDto
{
    public int OpenTasks { get; set; }
    public int OverdueTasks { get; set; }
    public int ActivitiesToday { get; set; }
    public int UnreadNotifications { get; set; }
    public int RenewalsDue { get; set; }
    public int OpenServiceRequests { get; set; }
    public int CompletedToday { get; set; }
}

public sealed class MyWorkbenchTaskDto
{
    public Guid TaskItemId { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TaskTypeCode { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string StageCode { get; set; } = string.Empty;
    public string? RelatedEntityName { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class MyWorkbenchScheduleEventDto
{
    public Guid EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? AccountName { get; set; }
    public string EventTypeCode { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string? Location { get; set; }
}

public sealed class MyWorkbenchActivityDto
{
    public Guid ActivityId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? AccountName { get; set; }
    public string ActivityTypeCode { get; set; } = string.Empty;
    public DateTime ActivityDateTime { get; set; }
    public string? Notes { get; set; }
}

public sealed class MyWorkbenchNotificationDto
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SeverityCode { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class MyWorkbenchQuickLinkDto
{
    public Guid QuickLinkId { get; set; }
    public Guid TenantId { get; set; }
    public string LinkCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string IconCssClass { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
