namespace Ams.Application.Common.Dtos;

public sealed class TaskTypeDto
{
    public Guid TaskTypeId { get; set; }
    public Guid TenantId { get; set; }
    public string TaskTypeCode { get; set; } = string.Empty;
    public string TaskTypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
    public Guid? ModifiedByUserId { get; set; }
    public bool IsDeleted { get; set; }
}
