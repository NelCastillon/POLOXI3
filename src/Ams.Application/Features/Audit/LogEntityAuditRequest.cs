using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Audit;

/// <summary>
/// Consolidated write contract for the global entity audit pipeline.
/// One request produces a single IAM.UserAuditTrail row plus one
/// Audit.AuditEvent (with AuditEventDetail / AuditEntityChange / AuditAlertEvent children)
/// per changed field, all resolved and enriched from the database in one transaction.
/// </summary>
public sealed class LogEntityAuditRequest
{
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>Actor user id resolved from claims or request arguments. Name/roles are resolved from IAM tables in the database.</summary>
    public Guid? ActorUserId { get; set; }

    /// <summary>Optional actor name hint from the request. Used only when the actor has no IAM.[User] row.</summary>
    [StringLength(300)]
    public string? ActorUserNameHint { get; set; }

    [Required]
    [StringLength(100)]
    public string ActionType { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string UserActionCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ActionCategory { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string ModuleName { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string EntityName { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    [StringLength(300)]
    public string? EntityDisplayName { get; set; }

    [StringLength(256)]
    public string? ParentEntityName { get; set; }

    public Guid? ParentEntityId { get; set; }

    /// <summary>Full JSON snapshot before the operation (null for creates).</summary>
    public string? OldValue { get; set; }

    /// <summary>Full JSON snapshot after the operation (request payload for deletes).</summary>
    public string? NewValue { get; set; }

    [StringLength(500)]
    public string? UserActionDescription { get; set; }

    [StringLength(64)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? UserAgent { get; set; }

    [StringLength(200)]
    public string? SessionId { get; set; }

    [StringLength(120)]
    public string? CorrelationId { get; set; }

    [StringLength(120)]
    public string? RequestId { get; set; }

    [Required]
    [StringLength(100)]
    public string SourceSystem { get; set; } = "API";

    [Required]
    [StringLength(50)]
    public string Severity { get; set; } = "Info";

    [Required]
    [StringLength(50)]
    public string StatusCode { get; set; } = "Success";

    public string? ErrorDetails { get; set; }

    public int? VersionNumber { get; set; }

    [StringLength(100)]
    public string? ControllerName { get; set; }

    [StringLength(100)]
    public string? ActionName { get; set; }

    [StringLength(10)]
    public string? HttpMethod { get; set; }

    /// <summary>Per-field changes; one enterprise audit event is written per entry.</summary>
    public List<EntityAuditFieldChange> Changes { get; set; } = [];
}

/// <summary>A single field-level change captured by the global entity audit filter.</summary>
public sealed class EntityAuditFieldChange
{
    public EntityAuditFieldChange()
    {
    }

    public EntityAuditFieldChange(string fieldName, string? oldValue, string? newValue, string dataTypeCode = "String")
    {
        FieldName = fieldName;
        OldValue = oldValue;
        NewValue = newValue;
        DataTypeCode = dataTypeCode;
    }

    [Required]
    [StringLength(256)]
    public string FieldName { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    [Required]
    [StringLength(50)]
    public string DataTypeCode { get; set; } = "String";

    /// <summary>Field-specific enterprise action type, e.g. CARRIER_NAME_CHANGED.</summary>
    [Required]
    [StringLength(100)]
    public string ActionType { get; set; } = string.Empty;

    /// <summary>True when this entry is a whole-snapshot fallback (create/delete), not a single field diff.</summary>
    public bool IsSnapshot { get; set; }
}
