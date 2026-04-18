namespace Ams.Application.Common.Dtos;

public sealed class PolicyAudienceDto
{
    public Guid     AudienceId       { get; set; }
    public Guid     PolicyDocumentId { get; set; }
    public string   TargetTypeCode   { get; set; } = string.Empty;
    public Guid?    TargetId         { get; set; }
    public string   TargetName       { get; set; } = string.Empty;
    public bool     IsRequired       { get; set; }
    public string?  AddedByFullName  { get; set; }
    public DateTime AddedDateUtc     { get; set; }
}
