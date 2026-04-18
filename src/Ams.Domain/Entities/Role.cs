using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class Role : AuditableEntity
{
    public string RoleCode { get; private set; } = string.Empty;
    public string RoleName { get; private set; } = string.Empty;
    public string RoleTypeCode { get; private set; } = "Internal";
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsBuiltIn { get; private set; }
    public bool IsSystemRole { get; private set; }
    public int SortOrder { get; private set; }

    private Role() { }

    public Role(Guid tenantId, string roleCode, string roleName, string roleTypeCode,
        bool isBuiltIn, bool isSystemRole, int sortOrder, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        RoleCode = roleCode;
        RoleName = roleName;
        RoleTypeCode = roleTypeCode;
        IsBuiltIn = isBuiltIn;
        IsSystemRole = isSystemRole;
        SortOrder = sortOrder;
    }

    public void Update(string roleName, string? description, int sortOrder, Guid? modifiedByUserId)
    {
        RoleName = roleName;
        Description = description;
        SortOrder = sortOrder;
        MarkModified(modifiedByUserId);
    }

    public void Activate(Guid? modifiedByUserId) { IsActive = true; MarkModified(modifiedByUserId); }
    public void Deactivate(Guid? modifiedByUserId) { IsActive = false; MarkModified(modifiedByUserId); }
}
