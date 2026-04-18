namespace Ams.Application.Features.Iam;

public sealed class CreateRoleRequest
{
    public Guid TenantId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string RoleTypeCode { get; set; } = "Internal";
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public int PrivilegeLevel { get; set; }
    public int SortOrder { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
