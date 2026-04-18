using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class Permission : AuditableEntity
{
    public string PermissionCode { get; private set; } = string.Empty;
    public string PermissionName { get; private set; } = string.Empty;
    public string ResourceCode { get; private set; } = string.Empty;
    public string ActionCode { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsBuiltIn { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Permission() { }

    public Permission(Guid tenantId, string permissionCode, string permissionName, string resourceCode, string actionCode, bool isBuiltIn, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        PermissionCode = permissionCode;
        PermissionName = permissionName;
        ResourceCode = resourceCode;
        ActionCode = actionCode;
        IsBuiltIn = isBuiltIn;
    }

    public void Deactivate(Guid? modifiedByUserId) { IsActive = false; MarkModified(modifiedByUserId); }
    public void Activate(Guid? modifiedByUserId) { IsActive = true; MarkModified(modifiedByUserId); }
}
