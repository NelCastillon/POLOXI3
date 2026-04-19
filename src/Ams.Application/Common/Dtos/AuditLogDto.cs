namespace Ams.Application.Common.Dtos;

public sealed class AuditLogDto
{
    public Guid AuditLogId { get; set; }
    public Guid? TenantId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EventTypeCode { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public Guid? PerformedByUserId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? RegionCode { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime PerformedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
