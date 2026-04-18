using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class ArAgingSnapshot : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public DateOnly SnapshotDate { get; private set; }
    public decimal CurrentAmount { get; private set; }
    public decimal Days30Amount { get; private set; }
    public decimal Days60Amount { get; private set; }
    public decimal Days90Amount { get; private set; }
    public decimal Days90PlusAmount { get; private set; }
    public decimal TotalOutstanding { get; private set; }

    private ArAgingSnapshot() { }

    public ArAgingSnapshot(Guid tenantId, Guid accountId, DateOnly snapshotDate, decimal currentAmount, decimal days30Amount, decimal days60Amount, decimal days90Amount, decimal days90PlusAmount, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        SnapshotDate = snapshotDate;
        CurrentAmount = currentAmount;
        Days30Amount = days30Amount;
        Days60Amount = days60Amount;
        Days90Amount = days90Amount;
        Days90PlusAmount = days90PlusAmount;
        TotalOutstanding = currentAmount + days30Amount + days60Amount + days90Amount + days90PlusAmount;
    }
}
