using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Audit;

public sealed class SearchUserAuditTrailRequest
{
    [Required]
    public Guid TenantId { get; set; }

    public Guid? UserId { get; set; }

    [StringLength(200)]
    public string? SearchTerm { get; set; }

    [StringLength(100)]
    public string? ActionCode { get; set; }

    [StringLength(50)]
    public string? CategoryCode { get; set; }

    [StringLength(50)]
    public string? SeverityCode { get; set; }

    [StringLength(50)]
    public string? StatusCode { get; set; }

    public DateTime? FromDateUtc { get; set; }

    public DateTime? ToDateUtc { get; set; }

    [Range(1, 100000)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 25;
}
