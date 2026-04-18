using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class RolePermission : AuditableEntity
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public Guid? GrantedByUserId { get; private set; }
    public DateTime GrantedDateUtc { get; private set; }

    private RolePermission() { }

    public RolePermission(Guid tenantId, Guid roleId, Guid permissionId, Guid? grantedByUserId)
        : base(tenantId, grantedByUserId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        GrantedByUserId = grantedByUserId;
        GrantedDateUtc = DateTime.UtcNow;
    }
}
