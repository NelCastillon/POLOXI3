using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Billing;

public sealed class CreateBillingAdjustmentRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid InvoiceId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    [Required]
    [StringLength(80)]
    public string AdjustmentTypeCode { get; set; } = "Credit";

    [Required]
    public DateOnly AdjustmentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Range(0.01, 100000000)]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public Guid? ApprovedByUserId { get; set; }

    public DateTime? ApprovedDateUtc { get; set; }

    [Required]
    [StringLength(50)]
    public string StatusCode { get; set; } = "Pending";

    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateBillingAdjustmentRequest
{
    [Required]
    public Guid AdjustmentId { get; set; }

    [Required]
    public Guid InvoiceId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    [Required]
    [StringLength(80)]
    public string AdjustmentTypeCode { get; set; } = "Credit";

    [Required]
    public DateOnly AdjustmentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Range(0.01, 100000000)]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public Guid? ApprovedByUserId { get; set; }

    public DateTime? ApprovedDateUtc { get; set; }

    [Required]
    [StringLength(50)]
    public string StatusCode { get; set; } = "Pending";

    public Guid? ModifiedByUserId { get; set; }
}
