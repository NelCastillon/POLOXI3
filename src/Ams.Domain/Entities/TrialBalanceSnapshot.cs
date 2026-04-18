using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class TrialBalanceSnapshot : AuditableEntity
{
    public DateOnly SnapshotDate { get; private set; }
    public Guid? AccountingPeriodId { get; private set; }
    public Guid GLAccountId { get; private set; }
    public string AccountCode { get; private set; } = string.Empty;
    public string AccountName { get; private set; } = string.Empty;
    public decimal DebitBalance { get; private set; }
    public decimal CreditBalance { get; private set; }
    public decimal NetBalance { get; private set; }

    private TrialBalanceSnapshot() { }

    public TrialBalanceSnapshot(Guid tenantId, DateOnly snapshotDate, Guid glAccountId, string accountCode, string accountName, decimal debitBalance, decimal creditBalance, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        SnapshotDate = snapshotDate;
        GLAccountId = glAccountId;
        AccountCode = accountCode;
        AccountName = accountName;
        DebitBalance = debitBalance;
        CreditBalance = creditBalance;
        NetBalance = debitBalance - creditBalance;
    }
}
