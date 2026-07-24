using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.PolicyCertificates;

public sealed record UpsertCertificateHolderRequest(
    [Required] Guid TenantId,
    Guid? CertificateHolderId,
    [Required, StringLength(60)] string HolderCode,
    [Required, StringLength(200)] string LegalName,
    [StringLength(200)] string? AddressLine1,
    [StringLength(200)] string? AddressLine2,
    [StringLength(100)] string? City,
    [StringLength(100)] string? StateProvince,
    [StringLength(30)] string? PostalCode,
    [StringLength(10)] string? CountryCode,
    [StringLength(160)] string? ContactName,
    [EmailAddress, StringLength(320)] string? EmailAddress,
    [Phone, StringLength(50)] string? PhoneNumber,
    [StringLength(50)] string? PreferredDeliveryMethodCode,
    string? DefaultWording,
    bool RequiresAdditionalInsured,
    bool RequiresWaiverOfSubrogation,
    bool RequiresPrimaryNonContributory,
    bool IsActive,
    Guid? UserId);

public sealed record CreateDocumentTemplateVersionRequest(
    [Required] Guid TenantId,
    [Required] Guid DocumentTemplateDefinitionId,
    [StringLength(50)] string? EditionCode,
    [Required, StringLength(50)] string ContentFormatCode,
    string? TemplateContent,
    [StringLength(1000)] string? StoragePath,
    [Required] string MergeFieldSchemaJson,
    [StringLength(1000)] string? ChangeSummary,
    [Required, StringLength(40)] string StatusCode,
    DateTime? EffectiveDateUtc,
    Guid? CreatedByUserId);

public sealed record CreateCertificateWorkflowRequest(
    [Required] Guid TenantId,
    Guid? PolicyId,
    [StringLength(80)] string? PolicyNumber,
    [Required] Guid CertificateHolderId,
    [Required, StringLength(80)] string RequestedDocumentTypeCode,
    string? RequestedWording,
    bool AdditionalInsured,
    bool WaiverOfSubrogation,
    bool PrimaryNonContributory,
    [Required, StringLength(50)] string SourceCode,
    [Required, StringLength(30)] string PriorityCode,
    DateTime? NeededByDateUtc,
    Guid? RequestedByUserId,
    [StringLength(200)] string? RequestedByName,
    [EmailAddress, StringLength(320)] string? RequestedByEmail);

public sealed record GenerateCertificateDocumentRequest(
    [Required] Guid TenantId,
    [Required] Guid CertificateId,
    [Required] Guid DocumentTemplateDefinitionId,
    Guid? DocumentTemplateVersionId,
    [Required] string MergeDataJson,
    [StringLength(1000)] string? ChangeSummary,
    Guid? UserId);

public sealed record QueueCertificateDeliveryRequest(
    [Required] Guid TenantId,
    [Required] Guid CertificateId,
    Guid? GeneratedDocumentVersionId,
    [Required, StringLength(50)] string DeliveryMethodCode,
    [StringLength(200)] string? RecipientName,
    [Required, StringLength(500)] string RecipientAddress,
    Guid? UserId);

public sealed record UpsertCertificateRenewalScheduleRequest(
    [Required] Guid TenantId,
    [Required] Guid CertificateId,
    Guid? CertificateHolderId,
    [Range(1, 365)] int RenewalLeadDays,
    DateTime NextRunDateUtc,
    bool AutoGenerate,
    bool AutoDeliver,
    Guid? UserId);