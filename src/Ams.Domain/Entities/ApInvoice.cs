using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class ApInvoice : AuditableEntity
{
    public Guid VendorId { get; private set; }
    public string InvoiceNumber { get; private set; } = string.Empty;
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal BalanceAmount { get; private set; }
    public ApInvoiceStatus Status { get; private set; } = ApInvoiceStatus.Open;
    public Guid? GLAccountId { get; private set; }
    public Guid? AgreementId { get; private set; }
    public string? Notes { get; private set; }

    private ApInvoice() { }

    public ApInvoice(Guid tenantId, Guid vendorId, string invoiceNumber, DateOnly invoiceDate, DateOnly dueDate, decimal totalAmount, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        VendorId = vendorId;
        InvoiceNumber = invoiceNumber;
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        TotalAmount = totalAmount;
        BalanceAmount = totalAmount;
        Status = ApInvoiceStatus.Open;
    }
}
