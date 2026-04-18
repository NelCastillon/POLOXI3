using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class InvoiceLine : AuditableEntity
{
    public Guid InvoiceId { get; private set; }
    public int LineOrder { get; private set; } = 1;
    public string? ItemCode { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; } = 1;
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal TaxPercent { get; private set; }
    public decimal LineTotal { get; private set; }
    public string? SourceEntityName { get; private set; }
    public Guid? SourceEntityId { get; private set; }

    private InvoiceLine() { }

    public InvoiceLine(Guid tenantId, Guid invoiceId, string description, decimal quantity, decimal unitPrice, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        InvoiceId = invoiceId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = quantity * unitPrice;
    }
}
