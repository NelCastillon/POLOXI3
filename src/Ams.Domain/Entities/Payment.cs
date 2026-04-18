using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class Payment : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public decimal Amount { get; private set; }
    public string PaymentMethodCode { get; private set; } = "ACH";
    public string? ReferenceNumber { get; private set; }
    public PaymentStatus Status { get; private set; } = PaymentStatus.Applied;
    public string? Notes { get; private set; }

    private Payment() { }

    public Payment(Guid tenantId, Guid accountId, Guid? invoiceId, DateOnly paymentDate, decimal amount, string paymentMethodCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        InvoiceId = invoiceId;
        PaymentDate = paymentDate;
        Amount = amount;
        PaymentMethodCode = paymentMethodCode;
        Status = PaymentStatus.Applied;
    }
}
