using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Audit;

public sealed class LogEnterpriseAuditEventRequest
{
    [Required]
    public Guid TenantId { get; set; }

    public Guid? ActorUserId { get; set; }

    [StringLength(300)]
    public string? ActorUserName { get; set; }

    [StringLength(200)]
    public string? ActorRole { get; set; }

    [Required]
    [StringLength(100)]
    public string ActorType { get; set; } = "User";

    [Required]
    [StringLength(100)]
    public string ActionType { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ActionCategory { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ModuleName { get; set; } = string.Empty;

    [StringLength(256)]
    public string? EntityName { get; set; }

    public Guid? EntityId { get; set; }

    [StringLength(300)]
    public string? EntityDisplayName { get; set; }

    [StringLength(256)]
    public string? ParentEntityName { get; set; }

    public Guid? ParentEntityId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    [StringLength(64)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? UserAgent { get; set; }

    [StringLength(120)]
    public string? CorrelationId { get; set; }

    [StringLength(120)]
    public string? RequestId { get; set; }

    [Required]
    [StringLength(100)]
    public string SourceSystem { get; set; } = "Web";

    [Required]
    [StringLength(50)]
    public string Severity { get; set; } = "Info";

    [Required]
    [StringLength(50)]
    public string StatusCode { get; set; } = "Success";

    public bool IsSensitiveData { get; set; }

    public bool IsPiiMasked { get; set; }

    public bool IsLegalHold { get; set; }

    [StringLength(500)]
    public string? ChangeReason { get; set; }

    public int? VersionNumber { get; set; }

    public string? MetadataJson { get; set; }

    [StringLength(200)]
    public string? DetailName { get; set; }

    [StringLength(50)]
    public string? DetailDataTypeCode { get; set; }
}
