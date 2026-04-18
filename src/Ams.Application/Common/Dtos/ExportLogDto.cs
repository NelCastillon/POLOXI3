namespace Ams.Application.Common.Dtos;

public sealed class ExportLogDto
{
    public Guid ExportLogId { get; set; }
    public Guid TenantId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string ExportTypeCode { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? FormatCode { get; set; }
    public int RecordCount { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
