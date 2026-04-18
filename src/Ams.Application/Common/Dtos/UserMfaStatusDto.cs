namespace Ams.Application.Common.Dtos;

public sealed class UserMfaStatusDto
{
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int DeviceCount { get; set; }
    public int VerifiedDeviceCount { get; set; }
    public bool HasActiveMfa { get; set; }
    public bool MfaRequired { get; set; }
    public DateTime? LastMfaUsedDateUtc { get; set; }
}
