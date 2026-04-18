namespace Ams.Domain.Entities;

public sealed class TrustedDevice
{
    public Guid TrustedDeviceId { get; private set; } = Guid.NewGuid();
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string DeviceName { get; private set; } = string.Empty;
    public string? DeviceFingerprint { get; private set; }
    public string? UserAgent { get; private set; }
    public string? IpAddress { get; private set; }
    public string? DeviceTypeCode { get; private set; }
    public string? BrowserName { get; private set; }
    public string? OperatingSystem { get; private set; }
    public DateTime TrustedDateUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ExpiresDateUtc { get; private set; }
    public int RiskScore { get; private set; }
    public string? RiskFlags { get; private set; }
    public string? RiskNotes { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? RevokedDateUtc { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public string? RevokedReason { get; private set; }
    public DateTime? LastSeenDateUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTime CreatedDateUtc { get; private set; } = DateTime.UtcNow;
    public Guid? ModifiedByUserId { get; private set; }
    public DateTime? ModifiedDateUtc { get; private set; }
    public bool IsDeleted { get; private set; }

    private TrustedDevice() { }

    public TrustedDevice(Guid tenantId, Guid userId, string deviceName, Guid? createdByUserId)
    {
        TenantId        = tenantId;
        UserId          = userId;
        DeviceName      = deviceName;
        CreatedByUserId = createdByUserId;
    }
}
