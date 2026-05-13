using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Billing;

public sealed class CreateTimeEntryRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    public Guid? EngagementId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Range(0.01, 24)]
    public decimal Hours { get; set; }

    [Range(0, 24)]
    public decimal BillableHours { get; set; }

    [Range(0, 100000)]
    public decimal RateAmount { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(50)]
    public string StatusCode { get; set; } = "Draft";

    public Guid? InvoiceId { get; set; }

    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateTimeEntryRequest
{
    [Required]
    public Guid TimeEntryId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    public Guid? EngagementId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Range(0.01, 24)]
    public decimal Hours { get; set; }

    [Range(0, 24)]
    public decimal BillableHours { get; set; }

    [Range(0, 100000)]
    public decimal RateAmount { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(50)]
    public string StatusCode { get; set; } = "Draft";

    public Guid? InvoiceId { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
