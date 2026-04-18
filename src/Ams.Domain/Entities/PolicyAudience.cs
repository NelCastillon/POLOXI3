namespace Ams.Domain.Entities;

public sealed class PolicyAudience
{
    public Guid      AudienceId         { get; set; }
    public Guid      PolicyDocumentId   { get; set; }
    public string    TargetTypeCode     { get; set; } = string.Empty; // User | Role | AllUsers
    public Guid?     TargetId           { get; set; }
    public string    TargetName         { get; set; } = string.Empty;
    public bool      IsRequired         { get; set; } = true;
    public Guid?     AddedByUserId      { get; set; }
    public DateTime  AddedDateUtc       { get; set; }
    public bool      IsDeleted          { get; set; }
}
