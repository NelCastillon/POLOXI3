namespace Ams.Application.Common.Dtos;

public sealed class CashReceiptEntryDto
{
    public Guid CashReceiptEntryId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? InvoiceId { get; set; }
    public DateOnly ReceiptDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethodCode { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public Guid? GLAccountId { get; set; }
    public string? BankAccountCode { get; set; }
    public string? Notes { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
