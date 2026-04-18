using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class CommissionClawback : AuditableEntity
{
    public Guid PayeeId { get; private set; }
    public Guid OriginalTransactionId { get; private set; }
    public DateOnly ClawbackDate { get; private set; }
    public decimal Amount { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedDateUtc { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;

    private CommissionClawback() { }

    public CommissionClawback(Guid tenantId, Guid payeeId, Guid originalTransactionId, DateOnly clawbackDate, decimal amount, string reasonCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        PayeeId = payeeId;
        OriginalTransactionId = originalTransactionId;
        ClawbackDate = clawbackDate;
        Amount = amount;
        ReasonCode = reasonCode;
        StatusCode = "Pending";
    }
}
