using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class Invoice : AuditableEntity
{
    public string InvoiceNumber { get; private set; } = string.Empty;
    public Guid AccountId { get; private set; }
    public Guid? AgreementId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal BalanceAmount { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public InvoiceStatus Status { get; private set; }

    private Invoice() { }

    public Invoice(Guid tenantId, string invoiceNumber, Guid accountId, decimal totalAmount, DateOnly invoiceDate, DateOnly dueDate, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        InvoiceNumber = invoiceNumber;
        AccountId = accountId;
        TotalAmount = totalAmount;
        BalanceAmount = totalAmount;
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        Status = InvoiceStatus.Draft;
    }
}
