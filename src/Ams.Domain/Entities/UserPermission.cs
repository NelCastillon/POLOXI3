using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class UserPermission : AuditableEntity
{
    public Guid UserId { get; private set; }
    public Guid PermissionId { get; private set; }
    public bool IsGranted { get; private set; } = true;
    public Guid? GrantedByUserId { get; private set; }
    public DateTime GrantedDateUtc { get; private set; }
    public DateTime? ExpiresDateUtc { get; private set; }

    private UserPermission() { }

    public UserPermission(Guid tenantId, Guid userId, Guid permissionId, bool isGranted,
        Guid? grantedByUserId, DateTime? expiresDateUtc = null)
        : base(tenantId, grantedByUserId)
    {
        UserId = userId;
        PermissionId = permissionId;
        IsGranted = isGranted;
        GrantedByUserId = grantedByUserId;
        GrantedDateUtc = DateTime.UtcNow;
        ExpiresDateUtc = expiresDateUtc;
    }
}
