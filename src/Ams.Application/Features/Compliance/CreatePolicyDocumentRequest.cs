using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Compliance;

public sealed class CreatePolicyDocumentRequest
{
    public Guid      TenantId         { get; set; }
    [Required, StringLength(50)]
    public string    PolicyCode       { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string    PolicyTitle      { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string    PolicyTypeCode   { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string    Version          { get; set; } = "1.0";
    public DateTime? EffectiveDateUtc { get; set; }
    [StringLength(1000)]
    public string?   Description      { get; set; }
    public Guid?     OwnedByUserId    { get; set; }
    public Guid?     CreatedByUserId  { get; set; }
}
