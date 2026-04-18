using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class RetainerDrawdown : AuditableEntity
{
    public Guid RetainerAccountId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public DateOnly DrawdownDate { get; private set; }
    public decimal Amount { get; private set; }
    public string? Description { get; private set; }

    private RetainerDrawdown() { }

    public RetainerDrawdown(Guid tenantId, Guid retainerAccountId, DateOnly drawdownDate, decimal amount, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        RetainerAccountId = retainerAccountId;
        DrawdownDate = drawdownDate;
        Amount = amount;
    }
}
