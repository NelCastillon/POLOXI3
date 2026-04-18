using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class CommissionPlan : AuditableEntity
{
    public string PlanCode { get; private set; } = string.Empty;
    public string PlanName { get; private set; } = string.Empty;
    public DateOnly EffectiveStartDate { get; private set; }

    private CommissionPlan() { }

    public CommissionPlan(Guid tenantId, string planCode, string planName, DateOnly effectiveStartDate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        PlanCode = planCode;
        PlanName = planName;
        EffectiveStartDate = effectiveStartDate;
    }
}
