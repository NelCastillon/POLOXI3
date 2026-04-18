using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class CommissionPlanVersion : AuditableEntity
{
    public Guid CommissionPlanId { get; private set; }
    public int VersionNumber { get; private set; }
    public string PlanName { get; private set; } = string.Empty;
    public decimal BaseRatePct { get; private set; }
    public DateOnly EffectiveStartDate { get; private set; }
    public DateOnly? EffectiveEndDate { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;

    private CommissionPlanVersion() { }

    public CommissionPlanVersion(Guid tenantId, Guid commissionPlanId, int versionNumber, string planName, decimal baseRatePct, DateOnly effectiveStartDate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        CommissionPlanId = commissionPlanId;
        VersionNumber = versionNumber;
        PlanName = planName;
        BaseRatePct = baseRatePct;
        EffectiveStartDate = effectiveStartDate;
        StatusCode = "Draft";
    }
}
