namespace Ams.Application.Common.Dtos;

public sealed class TrustedDeviceDto
{
    public Guid TrustedDeviceId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? Email { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string? DeviceFingerprint { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceTypeCode { get; set; }
    public string? BrowserName { get; set; }
    public string? OperatingSystem { get; set; }
    public DateTime TrustedDateUtc { get; set; }
    public DateTime? ExpiresDateUtc { get; set; }
    public int RiskScore { get; set; }
    public string? RiskFlags { get; set; }
    public string? RiskNotes { get; set; }
    public bool IsActive { get; set; }
    public DateTime? RevokedDateUtc { get; set; }
    public string? RevokedByUserName { get; set; }
    public string? RevokedReason { get; set; }
    public DateTime? LastSeenDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
