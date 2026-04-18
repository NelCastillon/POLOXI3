namespace Ams.Application.Common.Dtos;

public sealed class AcknowledgementDetailDto
{
    public Guid     AcknowledgementId   { get; set; }
    public Guid     PolicyDocumentId    { get; set; }
    public string   PolicyCode          { get; set; } = string.Empty;
    public string   PolicyTitle         { get; set; } = string.Empty;
    public string   PolicyTypeCode      { get; set; } = string.Empty;
    public string   Version             { get; set; } = string.Empty;
    public string   StatusCode          { get; set; } = string.Empty;
    public Guid     UserId              { get; set; }
    public string   UserFullName        { get; set; } = string.Empty;
    public string   UserEmail           { get; set; } = string.Empty;
    public DateTime AcknowledgedDateUtc { get; set; }
    public string?  Channel             { get; set; }
    public string?  IpAddress           { get; set; }
}
