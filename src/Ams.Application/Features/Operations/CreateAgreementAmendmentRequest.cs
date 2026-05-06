using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Operations;

public sealed class CreateAgreementAmendmentRequest
{
    public Guid TenantId { get; set; }
    [Required]
    public Guid AgreementId { get; set; }
    [Required, StringLength(50)]
    public string AmendmentNumber { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string AmendmentTypeCode { get; set; } = "Amendment";
    public DateOnly EffectiveDate { get; set; }
    [StringLength(1000)]
    public string? Description { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
