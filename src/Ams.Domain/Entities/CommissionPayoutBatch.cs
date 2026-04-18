using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class CommissionPayoutBatch : AuditableEntity
{
    public string BatchReference { get; private set; } = string.Empty;
    public DateOnly PayPeriodStart { get; private set; }
    public DateOnly PayPeriodEnd { get; private set; }
    public decimal TotalAmount { get; private set; }
    public int PayoutCount { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;
    public Guid? ProcessedByUserId { get; private set; }
    public DateTime? ProcessedDateUtc { get; private set; }

    private CommissionPayoutBatch() { }

    public CommissionPayoutBatch(Guid tenantId, string batchReference, DateOnly payPeriodStart, DateOnly payPeriodEnd, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        BatchReference = batchReference;
        PayPeriodStart = payPeriodStart;
        PayPeriodEnd = payPeriodEnd;
        TotalAmount = 0;
        PayoutCount = 0;
        StatusCode = "Pending";
    }
}
