using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class BadDebtEntry : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public DateOnly WriteOffDate { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid? GLAccountId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTime? ApprovedDateUtc { get; private set; }
    public string StatusCode { get; private set; } = "Pending";

    private BadDebtEntry() { }

    public BadDebtEntry(Guid tenantId, Guid accountId, DateOnly writeOffDate, decimal amount, string reason, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        WriteOffDate = writeOffDate;
        Amount = amount;
        Reason = reason;
        StatusCode = "Pending";
    }
}
