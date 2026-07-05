namespace Ams.Application.Common.Dtos;

public sealed class EnterpriseAuditAlertDto
{
    public Guid AuditAlertEventId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? AuditEventId { get; set; }
    public string AlertCode { get; set; } = string.Empty;
    public string AlertName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateTime CreatedUtc { get; set; }
}
