namespace Ams.Application.Features.Audit;

public sealed class LogAuditEventRequest
{
    public Guid TenantId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EventTypeCode { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public Guid? PerformedByUserId { get; set; }
}
