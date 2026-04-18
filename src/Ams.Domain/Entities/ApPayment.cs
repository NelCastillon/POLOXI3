using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class ApPayment : AuditableEntity
{
    public Guid VendorId { get; private set; }
    public Guid? ApInvoiceId { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public decimal Amount { get; private set; }
    public string PaymentMethodCode { get; private set; } = "ACH";
    public string? ReferenceNumber { get; private set; }
    public string? Notes { get; private set; }
    public string StatusCode { get; private set; } = "Issued";

    private ApPayment() { }

    public ApPayment(Guid tenantId, Guid vendorId, DateOnly paymentDate, decimal amount, string paymentMethodCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        VendorId = vendorId;
        PaymentDate = paymentDate;
        Amount = amount;
        PaymentMethodCode = paymentMethodCode;
        StatusCode = "Issued";
    }
}
