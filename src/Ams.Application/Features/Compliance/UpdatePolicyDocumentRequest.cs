using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Compliance;

public sealed class UpdatePolicyDocumentRequest
{
    [Required, StringLength(50)]
    public string    PolicyCode        { get; set; } = string.Empty;
    [Required, StringLength(200)]
    public string    PolicyTitle       { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string    PolicyTypeCode    { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string    Version           { get; set; } = "1.0";
    public DateTime? EffectiveDateUtc  { get; set; }
    [StringLength(1000)]
    public string?   Description       { get; set; }
    public string?   Content           { get; set; }
    public Guid?     OwnedByUserId     { get; set; }
    public Guid?     ModifiedByUserId  { get; set; }
}
