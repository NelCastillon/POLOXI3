namespace Ams.Application.Common.Dtos;

public sealed class EnterpriseAuditEventDto
{
    public Guid AuditEventId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorUserName { get; set; }
    public string? ActorRole { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string ActionCategory { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityDisplayName { get; set; }
    public string? ParentEntityName { get; set; }
    public Guid? ParentEntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public string? RequestId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? StatusCode { get; set; }
    public bool IsSensitiveData { get; set; }
    public bool IsPiiMasked { get; set; }
    public bool IsLegalHold { get; set; }
    public string? ChangeReason { get; set; }
    public int? VersionNumber { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedUtc { get; set; }
}
