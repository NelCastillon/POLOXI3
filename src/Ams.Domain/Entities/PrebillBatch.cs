using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class PrebillBatch : AuditableEntity
{
    public string BatchNumber { get; private set; } = string.Empty;
    public Guid? AccountId { get; private set; }
    public DateOnly BillingPeriodStart { get; private set; }
    public DateOnly BillingPeriodEnd { get; private set; }
    public decimal TotalAmount { get; private set; }
    public PrebillStatus Status { get; private set; } = PrebillStatus.Draft;
    public string? Notes { get; private set; }

    private PrebillBatch() { }

    public PrebillBatch(Guid tenantId, string batchNumber, DateOnly billingPeriodStart, DateOnly billingPeriodEnd, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        BatchNumber = batchNumber;
        BillingPeriodStart = billingPeriodStart;
        BillingPeriodEnd = billingPeriodEnd;
        Status = PrebillStatus.Draft;
    }
}
