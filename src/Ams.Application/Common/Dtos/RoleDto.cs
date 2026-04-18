namespace Ams.Application.Common.Dtos;

public sealed class RoleDto
{
    public Guid RoleId { get; set; }
    public Guid TenantId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string RoleTypeCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsBuiltIn { get; set; }
    public bool IsSystemRole { get; set; }
    public int PrivilegeLevel { get; set; }
    public int SortOrder { get; set; }
    public int PermissionCount { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}
