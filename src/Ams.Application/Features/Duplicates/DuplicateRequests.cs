using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Duplicates;

public sealed class DuplicateSearchRequest
{
    [Required]
    public Guid TenantId { get; set; }

    public string? EntityType { get; set; }

    public string? SearchTerm { get; set; }

    public string? ConfidenceBand { get; set; }

    public string? StatusCode { get; set; }

    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}

public sealed class DuplicateScanRequest
{
    [Required]
    public Guid TenantId { get; set; }

    public Guid? ScannedByUserId { get; set; }
}

public sealed class DuplicateSetPrimaryRequest
{
    [Required]
    public Guid PrimaryRecordId { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}

public sealed class DuplicateResolveRequest
{
    public Guid? ResolvedByUserId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class DuplicateBulkResolveRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    public IReadOnlyList<Guid> GroupIds { get; set; } = [];

    public Guid? ResolvedByUserId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
