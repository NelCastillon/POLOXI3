namespace Ams.Application.Features.Iam;

public sealed class AssignRolePermissionRequest
{
    public Guid TenantId { get; set; }
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public Guid? GrantedByUserId { get; set; }
}
