namespace Ams.Domain.Entities;

public sealed class PolicyAcknowledgement
{
    public Guid     AcknowledgementId   { get; set; }
    public Guid     PolicyDocumentId    { get; set; }
    public Guid     UserId              { get; set; }
    public Guid?    TenantId            { get; set; }
    public DateTime AcknowledgedDateUtc { get; set; }
    public string?  Channel             { get; set; }
    public string?  IpAddress           { get; set; }
    public DateTime CreatedDateUtc      { get; set; }
}
