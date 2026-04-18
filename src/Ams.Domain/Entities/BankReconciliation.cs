using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class BankReconciliation : AuditableEntity
{
    public string BankAccountCode { get; private set; } = string.Empty;
    public DateOnly StatementDate { get; private set; }
    public decimal StatementBalance { get; private set; }
    public decimal BookBalance { get; private set; }
    public string StatusCode { get; private set; } = "Open";
    public DateTime? ReconciledDateUtc { get; private set; }
    public Guid? ReconciledByUserId { get; private set; }

    private BankReconciliation() { }

    public BankReconciliation(Guid tenantId, string bankAccountCode, DateOnly statementDate, decimal statementBalance, decimal bookBalance, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        BankAccountCode = bankAccountCode;
        StatementDate = statementDate;
        StatementBalance = statementBalance;
        BookBalance = bookBalance;
        StatusCode = "Open";
    }
}
