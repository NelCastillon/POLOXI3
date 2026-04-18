using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class ApInvoiceLine : AuditableEntity
{
    public Guid ApInvoiceId { get; private set; }
    public int LineOrder { get; private set; } = 1;
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; } = 1;
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }
    public Guid? GLAccountId { get; private set; }

    private ApInvoiceLine() { }

    public ApInvoiceLine(Guid tenantId, Guid apInvoiceId, string description, decimal quantity, decimal unitPrice, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        ApInvoiceId = apInvoiceId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = quantity * unitPrice;
    }
}
