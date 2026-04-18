namespace Ams.Application.Features.Iam;

public sealed class UpdateRoleRequest
{
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PrivilegeLevel { get; set; }
    public int SortOrder { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}
