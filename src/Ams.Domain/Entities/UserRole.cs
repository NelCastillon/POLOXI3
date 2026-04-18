using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class UserRole : AuditableEntity
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid? AssignedByUserId { get; private set; }
    public DateTime AssignedDateUtc { get; private set; }
    public DateTime? EffectiveStartDateUtc { get; private set; }
    public DateTime? EffectiveEndDateUtc { get; private set; }
    public bool IsActive { get; private set; } = true;

    private UserRole() { }

    public UserRole(Guid tenantId, Guid userId, Guid roleId, Guid? assignedByUserId,
        DateTime? effectiveStartDateUtc = null, DateTime? effectiveEndDateUtc = null)
        : base(tenantId, assignedByUserId)
    {
        UserId = userId;
        RoleId = roleId;
        AssignedByUserId = assignedByUserId;
        AssignedDateUtc = DateTime.UtcNow;
        EffectiveStartDateUtc = effectiveStartDateUtc;
        EffectiveEndDateUtc = effectiveEndDateUtc;
    }

    public void Revoke(Guid? revokedByUserId)
    {
        IsActive = false;
        EffectiveEndDateUtc ??= DateTime.UtcNow;
        MarkModified(revokedByUserId);
    }
}
