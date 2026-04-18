using Ams.Domain.Common;

namespace Ams.Domain.Entities;

public sealed class CashReceiptEntry : AuditableEntity
{
    public Guid AccountId { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public DateOnly ReceiptDate { get; private set; }
    public decimal Amount { get; private set; }
    public string PaymentMethodCode { get; private set; } = "ACH";
    public string? ReferenceNumber { get; private set; }
    public Guid? GLAccountId { get; private set; }
    public string? BankAccountCode { get; private set; }
    public string? Notes { get; private set; }
    public string StatusCode { get; private set; } = "Posted";

    private CashReceiptEntry() { }

    public CashReceiptEntry(Guid tenantId, Guid accountId, DateOnly receiptDate, decimal amount, string paymentMethodCode, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        AccountId = accountId;
        ReceiptDate = receiptDate;
        Amount = amount;
        PaymentMethodCode = paymentMethodCode;
        StatusCode = "Posted";
    }
}
