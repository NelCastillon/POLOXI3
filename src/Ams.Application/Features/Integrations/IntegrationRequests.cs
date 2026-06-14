namespace Ams.Application.Features.Integrations;

using System.ComponentModel.DataAnnotations;

public sealed record CreateWebhookEndpointRequest(
    Guid TenantId,
    string Name,
    string TargetUrl,
    string[] EventTypes,
    string? Secret = null);

public sealed record UpdateWebhookEndpointRequest(
    string Name,
    string TargetUrl,
    string[] EventTypes,
    bool IsActive);

public sealed record CreateAutomationFlowRequest(
    Guid TenantId,
    string Name,
    string Description,
    string TriggerType);

public sealed record UpdateAutomationFlowRequest(
    string Name,
    string Description,
    string TriggerType,
    bool IsActive);

public sealed record SaveWorkflowDesignRequest(
    Guid TenantId,
    string Name,
    string Version,
    string DiagramJson);

public sealed record ResolveDownloadExceptionRequest(
    [Required]
    Guid ResolvedByUserId,

    [Required, StringLength(2000)]
    string ResolutionNote);

public sealed record CreateCarrierDownloadBatchRequest(
    [Required]
    Guid TenantId,

    Guid? CarrierId,

    [Required, StringLength(200)]
    string CarrierName,

    [Required, StringLength(50)]
    string SourceType,

    [StringLength(260)]
    string? FileName,

    [StringLength(1000)]
    string? RawStorageUri,

    Guid? CreatedByUserId = null);

public sealed record CreateCarrierDownloadItemRequest(
    [Required]
    Guid TenantId,

    [Required]
    Guid CarrierDownloadBatchId,

    [Required, StringLength(50)]
    string TransactionType,

    [StringLength(100)]
    string? CarrierPolicyNumber,

    [StringLength(300)]
    string? NamedInsured,

    DateOnly? EffectiveDate,

    DateOnly? ExpirationDate,

    [StringLength(100)]
    string? LineOfBusiness,

    [Range(0, 999999999)]
    decimal? Premium,

    [Range(0, 999999999)]
    decimal? Commission,

    string? RawPayload,

    string? NormalizedPayload,

    Guid? CreatedByUserId = null);

public sealed record CreateCarrierDownloadExceptionRequest(
    [Required]
    Guid TenantId,

    [Required]
    Guid CarrierDownloadItemId,

    [Required, StringLength(100)]
    string ExceptionType,

    [Required, StringLength(50)]
    string Severity,

    Guid? AssignedToUserId,

    [StringLength(2000)]
    string? ResolutionNotes,

    Guid? CreatedByUserId = null);

public sealed record ManualCarrierDownloadMatchRequest(
    [Required]
    Guid TenantId,

    Guid? MatchedAccountId,

    Guid? MatchedPolicyId,

    Guid? MatchedContactId,

    [Range(0, 100)]
    decimal MatchScore,

    [Required, StringLength(50)]
    string MatchMethod,

    [Required]
    Guid ReviewedByUserId,

    [StringLength(2000)]
    string? ResolutionNote);

public sealed record UpdateCarrierDownloadItemStatusRequest(
    [Required, StringLength(50)]
    string MatchStatus,

    [Required, StringLength(50)]
    string ProcessingStatus,

    [StringLength(2000)]
    string? ErrorMessage,

    Guid? ModifiedByUserId = null);

public sealed record CompleteCarrierDownloadBatchRequest(
    [Required, StringLength(50)]
    string Status,

    [Range(0, int.MaxValue)]
    int TotalRecords,

    [Range(0, int.MaxValue)]
    int ProcessedRecords,

    [Range(0, int.MaxValue)]
    int FailedRecords,

    [StringLength(2000)]
    string? ErrorMessage,

    Guid? ModifiedByUserId = null);
