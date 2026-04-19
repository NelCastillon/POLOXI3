namespace Ams.Application.Common.Dtos;

public sealed class BackgroundJobDto
{
    public Guid     BackgroundJobId { get; set; }
    public string   JobTypeCode     { get; set; } = string.Empty;
    public Guid?    TenantId        { get; set; }
    public string   StatusCode      { get; set; } = "Queued";
    public DateTime CreatedDateUtc  { get; set; }
    public DateTime? StartedDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public int      DurationMs      { get; set; }
    public int      RetryCount      { get; set; }
    public string?  CorrelationId   { get; set; }
    public string?  ErrorMessage    { get; set; }
    public string?  Payload         { get; set; }
    public string?  ResultSummary   { get; set; }
}
