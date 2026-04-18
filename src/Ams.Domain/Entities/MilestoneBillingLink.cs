using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class MilestoneBillingLink : AuditableEntity
{
    public Guid MilestoneId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public decimal BillingAmount { get; private set; }
    public DateTime? TriggeredDateUtc { get; private set; }
    public string StatusCode { get; private set; } = "Pending";
    public string? Notes { get; private set; }

    private MilestoneBillingLink() { }

    public MilestoneBillingLink(Guid tenantId, Guid milestoneId, decimal billingAmount, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        MilestoneId = milestoneId;
        BillingAmount = billingAmount;
        StatusCode = "Pending";
    }
}
