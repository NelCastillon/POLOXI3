using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class CommissionTransaction : AuditableEntity
{
    public Guid PayeeId { get; private set; }
    public Guid CommissionPlanId { get; private set; }
    public string SourceEntityName { get; private set; } = string.Empty;
    public Guid SourceEntityId { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal CommissionRate { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public CommissionTransactionStatus Status { get; private set; } = CommissionTransactionStatus.Pending;
    public Guid? PayoutId { get; private set; }

    private CommissionTransaction() { }

    public CommissionTransaction(Guid tenantId, Guid payeeId, Guid commissionPlanId, string sourceEntityName, Guid sourceEntityId, DateOnly transactionDate, decimal grossAmount, decimal commissionRate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        PayeeId = payeeId;
        CommissionPlanId = commissionPlanId;
        SourceEntityName = sourceEntityName;
        SourceEntityId = sourceEntityId;
        TransactionDate = transactionDate;
        GrossAmount = grossAmount;
        CommissionRate = commissionRate;
        CommissionAmount = grossAmount * commissionRate / 100m;
        Status = CommissionTransactionStatus.Pending;
    }
}
