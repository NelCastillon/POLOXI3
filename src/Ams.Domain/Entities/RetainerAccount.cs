using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class RetainerAccount : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public Guid? AgreementId { get; private set; }
    public string RetainerName { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public decimal UsedAmount { get; private set; }
    public decimal RemainingAmount { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly? PeriodEnd { get; private set; }
    public RetainerStatus Status { get; private set; } = RetainerStatus.Active;

    private RetainerAccount() { }

    public RetainerAccount(Guid tenantId, Guid accountId, string retainerName, decimal totalAmount, DateOnly periodStart, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        RetainerName = retainerName;
        TotalAmount = totalAmount;
        RemainingAmount = totalAmount;
        PeriodStart = periodStart;
        Status = RetainerStatus.Active;
    }
}
