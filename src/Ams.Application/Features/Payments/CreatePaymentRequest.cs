using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Payments;

public sealed class CreatePaymentRequest
{
    public Guid TenantId { get; set; }
    [Required]
    public Guid AccountId { get; set; }
    public Guid? InvoiceId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.Today;
    [Range(0.01, 999999999999)]
    public decimal Amount { get; set; }
    [Required, StringLength(50)]
    public string PaymentMethodCode { get; set; } = "ACH";
    [StringLength(100)]
    public string? ReferenceNumber { get; set; }
    [Required, StringLength(50)]
    public string StatusCode { get; set; } = "Pending";
    [StringLength(500)]
    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
