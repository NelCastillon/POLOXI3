namespace Ams.Application.Common.Dtos;

public sealed class ApInvoiceDto
{
    public Guid ApInvoiceId { get; set; }
    public Guid TenantId { get; set; }
    public Guid VendorId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal TaxAmount { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public Guid? GLAccountId { get; set; }
    public Guid? AgreementId { get; set; }
    public string? Notes { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
