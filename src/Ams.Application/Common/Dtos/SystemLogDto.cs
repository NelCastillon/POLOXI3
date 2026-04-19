namespace Ams.Application.Common.Dtos;

public sealed class SystemLogDto
{
    public Guid SystemLogId { get; set; }
    public Guid? TenantId { get; set; }
    public string LogLevel { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ExceptionText { get; set; }
    public string? StackTrace { get; set; }
    public string? RegionCode { get; set; }
    public string? CorrelationId { get; set; }
    public string? SourceContext { get; set; }
    public string? MachineName { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public int? HttpStatusCode { get; set; }
    public int? DurationMs { get; set; }
    public Guid? UserId { get; set; }
    public string? Properties { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
