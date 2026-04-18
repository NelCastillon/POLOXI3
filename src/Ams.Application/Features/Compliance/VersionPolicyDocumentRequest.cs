namespace Ams.Application.Features.Compliance;

public sealed class VersionPolicyDocumentRequest
{
    public string    NewVersion        { get; set; } = string.Empty;
    public DateTime? EffectiveDateUtc  { get; set; }
    public string?   Description       { get; set; }
    public Guid?     CreatedByUserId   { get; set; }
}
