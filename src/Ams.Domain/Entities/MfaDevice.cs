using Ams.Domain.Common;
using Ams.Domain.Enums;

namespace Ams.Domain.Entities;

public sealed class MfaDevice : AuditableEntity
{
    public Guid UserId { get; private set; }
    public MfaDeviceType DeviceType { get; private set; } = MfaDeviceType.TOTP;
    public string DeviceName { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string? EmailAddress { get; private set; }
    public string? SecretKeyHash { get; private set; }
    public bool IsVerified { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastUsedDateUtc { get; private set; }

    private MfaDevice() { }

    public MfaDevice(Guid tenantId, Guid userId, MfaDeviceType deviceType, string deviceName, Guid? createdByUserId)
        : base(tenantId, createdByUserId)
    {
        UserId = userId;
        DeviceType = deviceType;
        DeviceName = deviceName;
    }
}
