namespace Ams.Domain.Entities;

public sealed class SecurityEventLog
{
    public Guid SecurityEventId { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string EventTypeCode { get; private set; } = string.Empty;
    public string EventDescription { get; private set; } = string.Empty;
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public bool IsSuccess { get; private set; } = true;
    public int? RiskScore { get; private set; }
    public string? SessionId { get; private set; }
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public bool IsDeleted { get; private set; }

    private SecurityEventLog() { }

    public SecurityEventLog(Guid tenantId, string eventTypeCode, string eventDescription, bool isSuccess)
    {
        TenantId = tenantId;
        EventTypeCode = eventTypeCode;
        EventDescription = eventDescription;
        IsSuccess = isSuccess;
    }
}
