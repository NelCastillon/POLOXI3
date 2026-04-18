namespace Ams.Domain.Entities;

public sealed class ReportExecution
{
    public Guid ReportExecutionId { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid ReportDefinitionId { get; private set; }
    public Guid? ReportScheduleId { get; private set; }
    public string? Parameters { get; private set; }
    public string StatusCode { get; private set; } = "Queued";
    public string OutputFormat { get; private set; } = "PDF";
    public string? StoragePath { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public int? RowCount { get; private set; }
    public DateTime? StartedDateUtc { get; private set; }
    public DateTime? CompletedDateUtc { get; private set; }
    public string? ErrorMessage { get; private set; }
    public Guid? RequestedByUserId { get; private set; }
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public bool IsDeleted { get; private set; }

    private ReportExecution() { }

    public ReportExecution(Guid tenantId, Guid reportDefinitionId, string outputFormat, Guid? requestedByUserId)
    {
        TenantId = tenantId;
        ReportDefinitionId = reportDefinitionId;
        OutputFormat = outputFormat;
        RequestedByUserId = requestedByUserId;
    }
}
