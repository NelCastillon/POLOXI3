using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class CommissionCalculationResult : AuditableEntity
{
    public Guid TransactionId { get; private set; }
    public Guid PayeeId { get; private set; }
    public Guid CommissionPlanId { get; private set; }
    public decimal BaseAmount { get; private set; }
    public decimal RatePct { get; private set; }
    public decimal SplitPct { get; private set; }
    public decimal CalculatedAmount { get; private set; }
    public decimal? AdjustedAmount { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;
    public DateTime CalculatedDateUtc { get; private set; }

    private CommissionCalculationResult() { }

    public CommissionCalculationResult(Guid tenantId, Guid transactionId, Guid payeeId, Guid commissionPlanId, decimal baseAmount, decimal ratePct, decimal splitPct, decimal calculatedAmount, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        TransactionId = transactionId;
        PayeeId = payeeId;
        CommissionPlanId = commissionPlanId;
        BaseAmount = baseAmount;
        RatePct = ratePct;
        SplitPct = splitPct;
        CalculatedAmount = calculatedAmount;
        StatusCode = "Calculated";
        CalculatedDateUtc = DateTime.UtcNow;
    }
}
