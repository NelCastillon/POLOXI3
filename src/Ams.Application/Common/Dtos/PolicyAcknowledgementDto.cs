namespace Ams.Application.Common.Dtos;

public sealed class PolicyAcknowledgementDto
{
    public Guid     AcknowledgementId   { get; set; }
    public Guid     PolicyDocumentId    { get; set; }
    public Guid     UserId              { get; set; }
    public string   UserFullName        { get; set; } = string.Empty;
    public string   UserEmail           { get; set; } = string.Empty;
    public DateTime AcknowledgedDateUtc { get; set; }
    public string?  Channel             { get; set; }
    public string?  IpAddress           { get; set; }
}
