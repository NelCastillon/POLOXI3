using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class CommissionSplitRule : AuditableEntity
{
    public Guid CommissionPlanId { get; private set; }
    public string RuleName { get; private set; } = string.Empty;
    public string SplitTypeCode { get; private set; } = string.Empty;
    public Guid? PayeeId { get; private set; }
    public decimal SplitPct { get; private set; }
    public decimal? OverrideRatePct { get; private set; }
    public int Priority { get; private set; }
    public DateOnly EffectiveStartDate { get; private set; }
    public DateOnly? EffectiveEndDate { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;

    private CommissionSplitRule() { }

    public CommissionSplitRule(Guid tenantId, Guid commissionPlanId, string ruleName, string splitTypeCode, decimal splitPct, int priority, DateOnly effectiveStartDate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        CommissionPlanId = commissionPlanId;
        RuleName = ruleName;
        SplitTypeCode = splitTypeCode;
        SplitPct = splitPct;
        Priority = priority;
        EffectiveStartDate = effectiveStartDate;
        StatusCode = "Active";
    }
}
