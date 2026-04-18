using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class CommissionPayout : AuditableEntity
{
    public Guid PayeeId { get; private set; }
    public DateOnly PayoutDate { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string StatusCode { get; private set; } = "Pending";
    public DateTime? ProcessedDateUtc { get; private set; }
    public string? Notes { get; private set; }

    private CommissionPayout() { }

    public CommissionPayout(Guid tenantId, Guid payeeId, DateOnly payoutDate, decimal totalAmount, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        PayeeId = payeeId;
        PayoutDate = payoutDate;
        TotalAmount = totalAmount;
        StatusCode = "Pending";
    }
}
