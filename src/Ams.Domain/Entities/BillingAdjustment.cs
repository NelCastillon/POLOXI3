using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class BillingAdjustment : AuditableEntity
{
    public Guid InvoiceId { get; private set; }
    public Guid AccountId { get; private set; }
    public BillingAdjustmentType AdjustmentType { get; private set; } = BillingAdjustmentType.Credit;
    public DateOnly AdjustmentDate { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedDateUtc { get; private set; }
    public string StatusCode { get; private set; } = "Pending";

    private BillingAdjustment() { }

    public BillingAdjustment(Guid tenantId, Guid invoiceId, Guid accountId, BillingAdjustmentType adjustmentType, DateOnly adjustmentDate, decimal amount, string reason, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        InvoiceId = invoiceId;
        AccountId = accountId;
        AdjustmentType = adjustmentType;
        AdjustmentDate = adjustmentDate;
        Amount = amount;
        Reason = reason;
        StatusCode = "Pending";
    }
}
