using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Audit;

public sealed class SearchEnterpriseAuditEventsRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [StringLength(200)]
    public string? SearchTerm { get; set; }

    public Guid? ActorUserId { get; set; }

    [StringLength(100)]
    public string? ActorType { get; set; }

    [StringLength(100)]
    public string? ActionType { get; set; }

    [StringLength(100)]
    public string? ActionCategory { get; set; }

    [StringLength(100)]
    public string? ModuleName { get; set; }

    [StringLength(256)]
    public string? EntityName { get; set; }

    public Guid? EntityId { get; set; }

    [StringLength(50)]
    public string? Severity { get; set; }

    [StringLength(50)]
    public string? SourceSystem { get; set; }

    public bool? IsSensitiveData { get; set; }

    public bool? IsLegalHold { get; set; }

    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    [Range(1, 100000)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 25;
}
