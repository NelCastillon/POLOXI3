using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class RecordSecurityPolicy : AuditableEntity
{
    public Guid RoleId { get; private set; }
    public string EntityName { get; private set; } = string.Empty;
    public string PolicyTypeCode { get; private set; } = "Owner";
    public string? FilterExpression { get; private set; }
    public bool IsActive { get; private set; } = true;

    private RecordSecurityPolicy() { }

    public RecordSecurityPolicy(Guid tenantId, Guid roleId, string entityName, string policyTypeCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        RoleId = roleId;
        EntityName = entityName;
        PolicyTypeCode = policyTypeCode;
    }
}
