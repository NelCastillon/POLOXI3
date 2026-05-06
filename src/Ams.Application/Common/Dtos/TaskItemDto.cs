namespace Ams.Application.Common.Dtos;

public sealed class TaskItemDto
{
    public Guid TaskItemId { get; set; }
    public Guid TenantId { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TaskTypeCode { get; set; } = string.Empty;
    public string StageCode { get; set; } = string.Empty;
    public string PriorityCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? RelatedEntityName { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public Guid? ModifiedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}
