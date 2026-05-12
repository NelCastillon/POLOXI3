namespace Ams.Application.Common.Dtos;

public sealed class PaymentDto
{
    public Guid PaymentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? InvoiceId { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethodCode { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
