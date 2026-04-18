namespace Ams.Application.Common.Dtos;

public sealed class PolicyDocumentDto
{
    public Guid      PolicyDocumentId       { get; set; }
    public Guid      TenantId               { get; set; }
    public string    PolicyCode             { get; set; } = string.Empty;
    public string    PolicyTitle            { get; set; } = string.Empty;
    public string    PolicyTypeCode         { get; set; } = string.Empty;
    public string    Version                { get; set; } = "1.0";
    public DateTime? EffectiveDateUtc       { get; set; }
    public bool      IsActive               { get; set; }
    public string    StatusCode             { get; set; } = "Draft";
    public string?   Description            { get; set; }
    public string?   Content                { get; set; }
    public Guid?     OwnedByUserId          { get; set; }
    public string?   OwnedByFullName        { get; set; }
    public Guid?     ParentPolicyDocumentId { get; set; }
    public DateTime? PublishedDateUtc       { get; set; }
    public DateTime? RetiredDateUtc         { get; set; }
    public int       AcknowledgementCount   { get; set; }
    public DateTime  CreatedDateUtc         { get; set; }
    public DateTime? ModifiedDateUtc        { get; set; }
}
