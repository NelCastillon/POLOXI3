namespace Ams.Application.Common.Dtos;

public sealed class UserGroupDto
{
    public Guid UserGroupId { get; set; }
    public Guid TenantId { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string GroupTypeCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ManagerUserId { get; set; }
    public string? ManagerFullName { get; set; }
    public Guid? ParentGroupId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
