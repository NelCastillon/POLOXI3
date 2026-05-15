using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Operations;

public sealed class UpdateAgreementAmendmentRequest
{
    [Required]
    public Guid AgreementId { get; set; }

    [Required]
    [StringLength(50)]
    public string AmendmentNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string AmendmentTypeCode { get; set; } = "Amendment";

    [Required]
    public DateOnly EffectiveDate { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(50)]
    public string StatusCode { get; set; } = "Draft";

    public Guid? ModifiedByUserId { get; set; }
}
