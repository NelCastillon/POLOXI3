using Ams.Domain.Common;

namespace Ams.Domain.Entities;

/// <summary>
/// Policy-based access rule evaluated at operation time.
/// Examples: cannot approve own invoice, cannot delete after accounting close,
///           cannot export PII without privilege, cannot create and release the same batch.
/// SeverityCode: Block | Warn
/// </summary>
public sealed class SecurityPolicy : AuditableEntity
{
    public string PolicyCode { get; private set; } = string.Empty;
    public string PolicyName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string ResourceCode { get; private set; } = string.Empty;
    public string ActionCode { get; private set; } = string.Empty;
    public string ConditionExpression { get; private set; } = string.Empty;
    public string SeverityCode { get; private set; } = "Block";
    public string? ErrorMessageTemplate { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsSystemPolicy { get; private set; }

    private SecurityPolicy() { }

    public SecurityPolicy(Guid tenantId, string policyCode, string policyName, string resourceCode,
        string actionCode, string conditionExpression, string severityCode, bool isSystemPolicy,
        string? errorMessageTemplate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        PolicyCode = policyCode;
        PolicyName = policyName;
        ResourceCode = resourceCode;
        ActionCode = actionCode;
        ConditionExpression = conditionExpression;
        SeverityCode = severityCode;
        IsSystemPolicy = isSystemPolicy;
        ErrorMessageTemplate = errorMessageTemplate;
    }

    public void Update(string policyName, string? description, string conditionExpression,
        string severityCode, string? errorMessageTemplate, Guid? modifiedByUserId)
    {
        PolicyName = policyName;
        Description = description;
        ConditionExpression = conditionExpression;
        SeverityCode = severityCode;
        ErrorMessageTemplate = errorMessageTemplate;
        MarkModified(modifiedByUserId);
    }

    public void Activate(Guid? modifiedByUserId) { IsActive = true; MarkModified(modifiedByUserId); }
    public void Deactivate(Guid? modifiedByUserId) { IsActive = false; MarkModified(modifiedByUserId); }
}
