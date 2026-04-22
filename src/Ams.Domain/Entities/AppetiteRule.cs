using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class AppetiteRule : AuditableEntity
{
    public string RuleName        { get; private set; } = string.Empty;
    public string LobCode         { get; private set; } = string.Empty;
    public string? CarrierNaic    { get; private set; }
    public string RuleJson        { get; private set; } = "{}";
    public string AppetiteLevel   { get; private set; } = "Standard";
    public int    Priority        { get; private set; } = 100;
    public bool   IsActive        { get; private set; } = true;

    private AppetiteRule() { }

    public AppetiteRule(Guid tenantId, string ruleName, string lobCode, string ruleJson, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        RuleName = ruleName;
        LobCode  = lobCode;
        RuleJson = ruleJson;
    }
}
