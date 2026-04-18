using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class FieldSecurityPolicy : AuditableEntity
{
    public Guid RoleId { get; private set; }
    public string EntityName { get; private set; } = string.Empty;
    public string FieldName { get; private set; } = string.Empty;
    public bool CanRead { get; private set; } = true;
    public bool CanWrite { get; private set; }
    public bool IsHidden { get; private set; }

    private FieldSecurityPolicy() { }

    public FieldSecurityPolicy(Guid tenantId, Guid roleId, string entityName, string fieldName, bool canRead, bool canWrite, bool isHidden, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        RoleId = roleId;
        EntityName = entityName;
        FieldName = fieldName;
        CanRead = canRead;
        CanWrite = canWrite;
        IsHidden = isHidden;
    }
}
