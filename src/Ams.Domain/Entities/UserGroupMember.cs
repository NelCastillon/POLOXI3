using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class UserGroupMember : AuditableEntity
{
    public Guid UserGroupId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime JoinedDateUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? RemovedDateUtc { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid? AddedByUserId { get; private set; }

    private UserGroupMember() { }

    public UserGroupMember(Guid tenantId, Guid userGroupId, Guid userId, Guid? addedByUserId)
        : base(tenantId, addedByUserId)
    {
        UserGroupId = userGroupId;
        UserId = userId;
        AddedByUserId = addedByUserId;
        JoinedDateUtc = DateTime.UtcNow;
    }
}
