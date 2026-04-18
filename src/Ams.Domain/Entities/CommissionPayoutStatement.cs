using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class CommissionPayoutStatement : AuditableEntity
{
    public Guid PayeeId { get; private set; }
    public Guid? PayoutBatchId { get; private set; }
    public DateOnly StatementDate { get; private set; }
    public decimal GrossEarnings { get; private set; }
    public decimal TotalClawbacks { get; private set; }
    public decimal NetPayout { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public string StatusCode { get; private set; } = string.Empty;
    public DateTime? IssuedDateUtc { get; private set; }

    private CommissionPayoutStatement() { }

    public CommissionPayoutStatement(Guid tenantId, Guid payeeId, DateOnly statementDate, decimal grossEarnings, decimal totalClawbacks, string currencyCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        PayeeId = payeeId;
        StatementDate = statementDate;
        GrossEarnings = grossEarnings;
        TotalClawbacks = totalClawbacks;
        NetPayout = grossEarnings - totalClawbacks;
        CurrencyCode = currencyCode;
        StatusCode = "Draft";
    }
}
