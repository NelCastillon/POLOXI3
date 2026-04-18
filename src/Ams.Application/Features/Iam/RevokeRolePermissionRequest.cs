namespace Ams.Application.Features.Iam;

public sealed class RevokeRolePermissionRequest
{
    public Guid RolePermissionId { get; set; }
    public Guid? RevokedByUserId { get; set; }
}
