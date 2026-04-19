namespace Ams.Application.Common.Dtos;

public sealed class PlatformEventDto
{
    public Guid     PlatformEventId  { get; set; }
    public string   EventTypeCode    { get; set; } = string.Empty;
    public Guid?    TenantId         { get; set; }
    public string?  SourceService    { get; set; }
    public DateTime TimestampUtc     { get; set; }
    public string   ProcessingStatus { get; set; } = "Pending";
    public int      SubscriberCount  { get; set; }
    public string?  CorrelationId    { get; set; }
    public string?  Payload          { get; set; }
    public DateTime CreatedDateUtc   { get; set; }
}
