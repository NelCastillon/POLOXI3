namespace Ams.Domain.Entities;

public sealed class ExportLog
{
    public Guid ExportLogId { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public string EntityName { get; private set; } = string.Empty;
    public Guid? EntityId { get; private set; }
    public string ExportTypeCode { get; private set; } = string.Empty;
    public string? FileName { get; private set; }
    public string? FormatCode { get; private set; }
    public int RecordCount { get; private set; }
    public Guid? PerformedByUserId { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public bool IsDeleted { get; private set; }

    private ExportLog() { }

    public ExportLog(Guid tenantId, string entityName, string exportTypeCode, Guid? performedByUserId)
    {
        TenantId = tenantId;
        EntityName = entityName;
        ExportTypeCode = exportTypeCode;
        PerformedByUserId = performedByUserId;
    }
}
