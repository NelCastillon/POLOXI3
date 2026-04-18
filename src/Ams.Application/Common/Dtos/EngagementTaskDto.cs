namespace Ams.Application.Common.Dtos;

public sealed class EngagementTaskDto
{
    public Guid TaskId { get; set; }
    public Guid TenantId { get; set; }
    public Guid EngagementId { get; set; }
    public Guid? MilestoneId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public Guid? AssignedToUserId { get; set; }
    public DateOnly? DueDate { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
