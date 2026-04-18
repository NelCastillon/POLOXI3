namespace Ams.Application.Common.Dtos;

public sealed class ReportExecutionDto
{
    public Guid ReportExecutionId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ReportDefinitionId { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public Guid? ReportScheduleId { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string OutputFormat { get; set; } = string.Empty;
    public string? StoragePath { get; set; }
    public long? FileSizeBytes { get; set; }
    public int? RowCount { get; set; }
    public DateTime? StartedDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
