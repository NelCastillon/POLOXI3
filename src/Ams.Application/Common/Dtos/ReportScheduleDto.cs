namespace Ams.Application.Common.Dtos;

public sealed class ReportScheduleDto
{
    public Guid ReportScheduleId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ReportDefinitionId { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public string ModuleCode { get; set; } = string.Empty;
    public string FrequencyCode { get; set; } = string.Empty;
    public string OutputFormat { get; set; } = string.Empty;
    public string DeliveryEmail { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? NextRunDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed record ScheduleReportRequest(Guid TenantId, Guid ReportDefinitionId, string FrequencyCode, string OutputFormat, string DeliveryEmail);
public sealed record RunReportRequest(Guid TenantId, string OutputFormat, Guid? RequestedByUserId = null);
