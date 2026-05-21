using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Documents;

public sealed record CreateAcordFormRequest(
    Guid TenantId,
    [Required, StringLength(30)] string FormNumber,
    [Required, StringLength(200)] string FormName,
    [Required, StringLength(100)] string LineOfBusiness,
    [Required, StringLength(50)] string Edition,
    [Required, StringLength(50)] string Status,
    [StringLength(100)] string? PolicyNumber,
    bool AiPrefilled,
    [Range(0, 500)] int? PrefillFieldCount,
    [Range(0, 100)] int? PrefillConfidence,
    [StringLength(160)] string? OwnerName,
    [StringLength(1000)] string? Description,
    Guid? CreatedByUserId);

public sealed record UpdateAcordFormStatusRequest(
    Guid AcordFormId,
    [Required, StringLength(50)] string Status,
    Guid? ModifiedByUserId);

public sealed record PrefillAcordFormRequest(
    Guid AcordFormId,
    [StringLength(100)] string? PolicyNumber,
    [Range(0, 500)] int PrefillFieldCount,
    [Range(0, 100)] int PrefillConfidence,
    Guid? ModifiedByUserId);
