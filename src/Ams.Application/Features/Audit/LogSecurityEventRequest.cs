namespace Ams.Application.Features.Audit;

public sealed class LogSecurityEventRequest
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string EventTypeCode { get; set; } = string.Empty;
    public string EventDescription { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuccess { get; set; } = true;
    public int? RiskScore { get; set; }
    public string? SessionId { get; set; }
}
