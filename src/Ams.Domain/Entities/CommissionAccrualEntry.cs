using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class CommissionAccrualEntry : AuditableEntity
{
    public Guid TransactionId { get; private set; }
    public Guid? GLAccountId { get; private set; }
    public DateOnly AccrualDate { get; private set; }
    public decimal AccruedAmount { get; private set; }
    public DateOnly? ReversalDate { get; private set; }
    public decimal? ReversedAmount { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public string StatusCode { get; private set; } = string.Empty;

    private CommissionAccrualEntry() { }

    public CommissionAccrualEntry(Guid tenantId, Guid transactionId, DateOnly accrualDate, decimal accruedAmount, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        TransactionId = transactionId;
        AccrualDate = accrualDate;
        AccruedAmount = accruedAmount;
        StatusCode = "Accrued";
    }
}
