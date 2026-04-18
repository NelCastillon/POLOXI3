using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class CommissionDispute : AuditableEntity
{
    public Guid PayeeId { get; private set; }
    public Guid? TransactionId { get; private set; }
    public DateOnly DisputeDate { get; private set; }
    public string DisputeReason { get; private set; } = string.Empty;
    public decimal DisputedAmount { get; private set; }
    public string? Resolution { get; private set; }
    public Guid? ResolvedByUserId { get; private set; }
    public DateTime? ResolvedDateUtc { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;

    private CommissionDispute() { }

    public CommissionDispute(Guid tenantId, Guid payeeId, DateOnly disputeDate, string disputeReason, decimal disputedAmount, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        PayeeId = payeeId;
        DisputeDate = disputeDate;
        DisputeReason = disputeReason;
        DisputedAmount = disputedAmount;
        StatusCode = "Open";
    }
}
