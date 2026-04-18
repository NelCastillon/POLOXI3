namespace Ams.Domain.Entities;

public sealed class PolicyDocument
{
    public Guid      PolicyDocumentId       { get; set; }
    public Guid      TenantId               { get; set; }
    public string    PolicyCode             { get; set; } = string.Empty;
    public string    PolicyTitle            { get; set; } = string.Empty;
    public string    PolicyTypeCode         { get; set; } = string.Empty;
    public string    Version                { get; set; } = "1.0";
    public DateTime? EffectiveDateUtc       { get; set; }
    public bool      IsActive               { get; set; } = true;
    public string    StatusCode             { get; set; } = "Draft"; // Draft | Published | Retired
    public string?   Description            { get; set; }
    public string?   Content                { get; set; }
    public Guid?     OwnedByUserId          { get; set; }
    public Guid?     ParentPolicyDocumentId { get; set; }
    public Guid?     PublishedByUserId      { get; set; }
    public DateTime? PublishedDateUtc       { get; set; }
    public Guid?     RetiredByUserId        { get; set; }
    public DateTime? RetiredDateUtc         { get; set; }
    public Guid?     CreatedByUserId        { get; set; }
    public DateTime  CreatedDateUtc         { get; set; }
    public DateTime? ModifiedDateUtc        { get; set; }
    public bool      IsDeleted              { get; set; }
}
