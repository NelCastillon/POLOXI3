using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class SegregationOfDutyRule : AuditableEntity
{
    public string RuleCode { get; private set; } = string.Empty;
    public string RuleName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string RoleACode { get; private set; } = string.Empty;
    public string RoleBCode { get; private set; } = string.Empty;
    public string SeverityCode { get; private set; } = "Hard";
    public bool IsActive { get; private set; } = true;
    public bool IsSystemDefined { get; private set; }

    private SegregationOfDutyRule() { }

    public SegregationOfDutyRule(Guid? tenantId, string ruleCode, string ruleName, string roleACode, string roleBCode, string severityCode, Guid? createdByUserId)
        : base(tenantId ?? Guid.Empty, createdByUserId)
    {
        RuleCode = ruleCode;
        RuleName = ruleName;
        RoleACode = roleACode;
        RoleBCode = roleBCode;
        SeverityCode = severityCode;
    }
}
