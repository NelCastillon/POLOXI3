using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class UserGroup : AuditableEntity
{
    public string GroupCode { get; private set; } = string.Empty;
    public string GroupName { get; private set; } = string.Empty;
    public UserGroupType GroupType { get; private set; } = UserGroupType.Internal;
    public string? Description { get; private set; }
    public Guid? ManagerUserId { get; private set; }
    public Guid? ParentGroupId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private UserGroup() { }

    public UserGroup(Guid tenantId, string groupCode, string groupName, UserGroupType groupType, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        GroupCode = groupCode;
        GroupName = groupName;
        GroupType = groupType;
    }
}
