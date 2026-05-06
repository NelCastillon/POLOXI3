using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Operations;

public sealed class CreateAgreementRequest
{
    public Guid TenantId { get; set; }
    [Required]
    public Guid AccountId { get; set; }
    [Required, StringLength(50)]
    public string AgreementNumber { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string AgreementTypeCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    [StringLength(1000)]
    public string? Description { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
