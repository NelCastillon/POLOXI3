using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Compliance;

public sealed class VersionPolicyDocumentRequest
{
    [Required, StringLength(50)]
    public string    NewVersion        { get; set; } = string.Empty;
    public DateTime? EffectiveDateUtc  { get; set; }
    [StringLength(1000)]
    public string?   Description       { get; set; }
    public Guid?     CreatedByUserId   { get; set; }
}
