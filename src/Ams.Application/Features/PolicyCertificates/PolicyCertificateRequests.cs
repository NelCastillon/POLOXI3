using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.PolicyCertificates;

public sealed record CreatePolicyCertificateRequest(
    [Required] Guid TenantId,
    Guid? PolicyId,
    [Required, StringLength(80)] string PolicyNumber,
    [Required, StringLength(200)] string AccountName,
    [Required, StringLength(200)] string HolderName,
    [StringLength(300)] string HolderAddress,
    [Required, StringLength(50)] string CertificateType,
    DateTime IssuedDate,
    DateTime ExpirationDate,
    [Required, StringLength(100)] string LineOfBusiness,
    [Required, StringLength(150)] string IssuedBy,
    [Required, StringLength(30)] string Status,
    bool AdditionalInsured,
    bool WaiverSubrogation,
    [StringLength(2000)] string Description,
    Guid? CreatedByUserId);

public sealed record UpdatePolicyCertificateRequest(
    [Required] Guid TenantId,
    Guid? PolicyId,
    [Required, StringLength(80)] string PolicyNumber,
    [Required, StringLength(200)] string AccountName,
    [Required, StringLength(200)] string HolderName,
    [StringLength(300)] string HolderAddress,
    [Required, StringLength(50)] string CertificateType,
    DateTime IssuedDate,
    DateTime ExpirationDate,
    [Required, StringLength(100)] string LineOfBusiness,
    [Required, StringLength(150)] string IssuedBy,
    [Required, StringLength(30)] string Status,
    bool AdditionalInsured,
    bool WaiverSubrogation,
    [StringLength(2000)] string Description,
    Guid? ModifiedByUserId);

public sealed record RevokePolicyCertificateRequest(
    [Required] Guid TenantId,
    Guid? RevokedByUserId,
    [StringLength(500)] string? Reason);

public sealed record RestorePolicyCertificateRequest(
    [Required] Guid TenantId,
    Guid? ModifiedByUserId);

public sealed record PolicyCertificateActionRequest(
    [Required] Guid TenantId,
    Guid? ModifiedByUserId);
