using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Billing;

public sealed class CreateExpenseEntryRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    public Guid? EngagementId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public DateOnly ExpenseDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    [StringLength(80)]
    public string CategoryCode { get; set; } = string.Empty;

    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool IsBillable { get; set; } = true;

    [Required]
    [StringLength(50)]
    public string StatusCode { get; set; } = "Draft";

    public Guid? InvoiceId { get; set; }

    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateExpenseEntryRequest
{
    [Required]
    public Guid ExpenseId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    public Guid? EngagementId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public DateOnly ExpenseDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    [StringLength(80)]
    public string CategoryCode { get; set; } = string.Empty;

    [Range(0.01, 1000000)]
    public decimal Amount { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public bool IsBillable { get; set; } = true;

    [Required]
    [StringLength(50)]
    public string StatusCode { get; set; } = "Draft";

    public Guid? InvoiceId { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
