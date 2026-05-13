using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Billing;

public sealed class CreateArAgingSnapshotRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    [Required]
    public DateOnly SnapshotDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Range(0, 100000000)]
    public decimal CurrentAmount { get; set; }

    [Range(0, 100000000)]
    public decimal Days30Amount { get; set; }

    [Range(0, 100000000)]
    public decimal Days60Amount { get; set; }

    [Range(0, 100000000)]
    public decimal Days90Amount { get; set; }

    [Range(0, 100000000)]
    public decimal Days90PlusAmount { get; set; }

    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateArAgingSnapshotRequest
{
    [Required]
    public Guid SnapshotId { get; set; }

    [Required]
    public Guid AccountId { get; set; }

    [Required]
    public DateOnly SnapshotDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Range(0, 100000000)]
    public decimal CurrentAmount { get; set; }

    [Range(0, 100000000)]
    public decimal Days30Amount { get; set; }

    [Range(0, 100000000)]
    public decimal Days60Amount { get; set; }

    [Range(0, 100000000)]
    public decimal Days90Amount { get; set; }

    [Range(0, 100000000)]
    public decimal Days90PlusAmount { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
