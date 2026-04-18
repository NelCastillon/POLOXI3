namespace Ams.Application.Common.Dtos;

public sealed class ApPaymentDto
{
    public Guid ApPaymentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid VendorId { get; set; }
    public Guid? ApInvoiceId { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethodCode { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
