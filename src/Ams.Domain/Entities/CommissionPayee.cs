using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class CommissionPayee : AuditableEntity
{
    public Guid UserId { get; private set; }
    public Guid CommissionPlanId { get; private set; }
    public string PayeeTypeCode { get; private set; } = "SalesRep";
    public decimal SplitPercentage { get; private set; } = 100m;
    public DateOnly EffectiveDate { get; private set; }
    public string StatusCode { get; private set; } = "Active";

    private CommissionPayee() { }

    public CommissionPayee(Guid tenantId, Guid userId, Guid commissionPlanId, decimal splitPercentage, DateOnly effectiveDate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        UserId = userId;
        CommissionPlanId = commissionPlanId;
        SplitPercentage = splitPercentage;
        EffectiveDate = effectiveDate;
    }
}
