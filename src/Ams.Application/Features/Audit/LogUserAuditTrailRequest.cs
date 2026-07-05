namespace Ams.Application.Features.Audit;

public sealed class LogUserAuditTrailRequest
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public string? ActionDescription { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid? ChangedByUserId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public string StatusCode { get; set; } = "Success";
    public string? ErrorDetails { get; set; }
}
