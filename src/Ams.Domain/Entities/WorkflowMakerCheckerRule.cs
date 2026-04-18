using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class WorkflowMakerCheckerRule : AuditableEntity
{
    public string EntityName { get; private set; } = string.Empty;
    public string OperationCode { get; private set; } = string.Empty;
    public bool RequiresDifferentUser { get; private set; }
    public string? MakerRoleCode { get; private set; }
    public string? CheckerRoleCode { get; private set; }
    public Guid? WorkflowDefinitionId { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsSystemDefined { get; private set; }

    private WorkflowMakerCheckerRule() { }

    public WorkflowMakerCheckerRule(Guid? tenantId, string entityName, string operationCode, Guid? createdByUserId)
        : base(tenantId ?? Guid.Empty, createdByUserId)
    {
        EntityName = entityName;
        OperationCode = operationCode;
        RequiresDifferentUser = true;
        IsActive = true;
    }
}
