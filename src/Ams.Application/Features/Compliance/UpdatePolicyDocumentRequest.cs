namespace Ams.Application.Features.Compliance;

public sealed class UpdatePolicyDocumentRequest
{
    public string    PolicyCode        { get; set; } = string.Empty;
    public string    PolicyTitle       { get; set; } = string.Empty;
    public string    PolicyTypeCode    { get; set; } = string.Empty;
    public string    Version           { get; set; } = "1.0";
    public DateTime? EffectiveDateUtc  { get; set; }
    public string?   Description       { get; set; }
    public string?   Content           { get; set; }
    public Guid?     OwnedByUserId     { get; set; }
    public Guid?     ModifiedByUserId  { get; set; }
}
