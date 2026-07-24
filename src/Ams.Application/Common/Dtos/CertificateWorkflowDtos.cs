namespace Ams.Application.Common.Dtos;

public sealed record CertificateWorkflowOptionDto(
    Guid CertificateWorkflowOptionId,
    Guid TenantId,
    string OptionGroupCode,
    string OptionCode,
    string DisplayName,
    string? Description,
    bool IsDefault,
    bool IsActive,
    int SortOrder);

public sealed record CertificateHolderDto(
    Guid CertificateHolderId,
    Guid TenantId,
    string HolderCode,
    string LegalName,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? StateProvince,
    string? PostalCode,
    string? CountryCode,
    string? ContactName,
    string? EmailAddress,
    string? PhoneNumber,
    string? PreferredDeliveryMethodCode,
    string? DefaultWording,
    bool RequiresAdditionalInsured,
    bool RequiresWaiverOfSubrogation,
    bool RequiresPrimaryNonContributory,
    bool IsActive,
    DateTime CreatedDateUtc,
    DateTime? ModifiedDateUtc);

public sealed record DocumentTemplateDefinitionDto(
    Guid DocumentTemplateDefinitionId,
    Guid TenantId,
    string TemplateCode,
    string TemplateName,
    string DocumentTypeCode,
    string? FormNumber,
    string? LineOfBusinessCode,
    string? Description,
    bool IsLicensedContent,
    bool IsActive,
    int CurrentVersionNumber,
    IReadOnlyList<DocumentTemplateVersionDto> Versions);

public sealed record DocumentTemplateVersionDto(
    Guid DocumentTemplateVersionId,
    Guid TenantId,
    Guid DocumentTemplateDefinitionId,
    int VersionNumber,
    string? EditionCode,
    string ContentFormatCode,
    string? TemplateContent,
    string? StoragePath,
    string MergeFieldSchemaJson,
    string? ChangeSummary,
    string StatusCode,
    DateTime? EffectiveDateUtc,
    DateTime? RetiredDateUtc,
    DateTime CreatedDateUtc);

public sealed record CertificateRequestDto(
    Guid CertificateRequestId,
    Guid TenantId,
    string RequestNumber,
    Guid? PolicyId,
    string? PolicyNumber,
    Guid? CertificateHolderId,
    string? HolderName,
    string RequestedDocumentTypeCode,
    string? RequestedWording,
    bool AdditionalInsured,
    bool WaiverOfSubrogation,
    bool PrimaryNonContributory,
    string SourceCode,
    string StatusCode,
    string PriorityCode,
    DateTime? NeededByDateUtc,
    Guid? RequestedByUserId,
    string? RequestedByName,
    string? RequestedByEmail,
    Guid? AssignedToUserId,
    Guid? CompletedCertificateId,
    DateTime SubmittedDateUtc,
    DateTime? CompletedDateUtc);

public sealed record GeneratedDocumentDto(
    Guid GeneratedDocumentId,
    Guid TenantId,
    string DocumentNumber,
    string DocumentTypeCode,
    string EntityTypeCode,
    Guid? EntityId,
    Guid TemplateDefinitionId,
    int CurrentVersionNumber,
    string StatusCode,
    IReadOnlyList<GeneratedDocumentVersionDto> Versions);

public sealed record GeneratedDocumentVersionDto(
    Guid GeneratedDocumentVersionId,
    Guid TenantId,
    Guid GeneratedDocumentId,
    Guid DocumentTemplateVersionId,
    int VersionNumber,
    string MergeDataJson,
    byte[]? RenderedContent,
    string? StoragePath,
    string ContentType,
    string? ContentHash,
    long? FileSizeBytes,
    string? ChangeSummary,
    DateTime CreatedDateUtc);

public sealed record CertificateDeliveryDto(
    Guid CertificateDeliveryId,
    Guid TenantId,
    Guid CertificateId,
    Guid? GeneratedDocumentVersionId,
    string DeliveryMethodCode,
    string? RecipientName,
    string RecipientAddress,
    string StatusCode,
    string? ProviderMessageId,
    DateTime QueuedDateUtc,
    DateTime? SentDateUtc,
    DateTime? DeliveredDateUtc,
    DateTime? FailedDateUtc,
    string? FailureReason,
    int AttemptCount);

public sealed record CertificateRenewalScheduleDto(
    Guid CertificateRenewalScheduleId,
    Guid TenantId,
    Guid CertificateId,
    Guid? CertificateHolderId,
    int RenewalLeadDays,
    DateTime NextRunDateUtc,
    string StatusCode,
    bool AutoGenerate,
    bool AutoDeliver,
    DateTime? LastRunDateUtc,
    string? LastResultCode,
    string? LastError,
    DateTime? LockedUntilDateUtc);

public sealed record CertificateAuditEventDto(
    Guid CertificateAuditEventId,
    Guid TenantId,
    Guid? CertificateId,
    Guid? CertificateRequestId,
    string EventTypeCode,
    string EventDescription,
    string? OldValueJson,
    string? NewValueJson,
    Guid? ActorUserId,
    string? ActorName,
    Guid CorrelationId,
    DateTime CreatedDateUtc);

public sealed record CertificateWorkflowWorkspaceDto(
    IReadOnlyList<CertificateWorkflowOptionDto> Options,
    IReadOnlyList<CertificateHolderDto> Holders,
    IReadOnlyList<DocumentTemplateDefinitionDto> Templates,
    IReadOnlyList<CertificateRequestDto> Requests,
    IReadOnlyList<CertificateRenewalScheduleDto> RenewalSchedules);

public sealed record CertificateGenerationResultDto(
    Guid CertificateId,
    Guid GeneratedDocumentId,
    Guid GeneratedDocumentVersionId,
    int VersionNumber,
    string ContentType,
    byte[] Content);